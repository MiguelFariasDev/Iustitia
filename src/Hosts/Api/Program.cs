using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.Modules.CRM.Api;
using Advocacia.Modules.CRM.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Legal.Api;
using Advocacia.Modules.Legal.Infrastructure;
using Advocacia.Modules.Workflow.Api;
using Advocacia.Modules.Atendimentos.Api;
using Advocacia.Modules.Atendimentos.Infrastructure;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Legal.Api;
using Advocacia.Modules.Legal.Infrastructure;
using Advocacia.Modules.Workflow.Infrastructure;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Api.Middleware;
using Advocacia.Platform.Application;
using Advocacia.Platform.Infrastructure;
using Advocacia.Platform.Infrastructure.Auth;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Advocacia.Platform.Infrastructure.Jobs.HangfireConfiguration;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

// TODO (Fase 4): registrar os módulos de negócio restantes (Documents, Reporting, ...) conforme entram no roadmap

var builder = WebApplication.CreateBuilder(args);

// No-op em dev (sem KeyVault:Uri configurado) — em produção, resolve segredos via Managed
// Identity (ver ADR-036/docs/compliance/production-config.md).
builder.Configuration.AddAdvocaciaKeyVault();

// AddPlatformAuth precisa vir depois de AddPlatformInfrastructure: ICurrentUser/ITenantContext
// reais (baseados em HttpContext/JWT) substituem os padrões seguros (Anonymous/AsyncLocal) —
// ver Platform.Infrastructure/Auth/DependencyInjection.cs. AddPlatformApplication (MediatR,
// FluentValidation, Mapster) é independente de ASP.NET Core — ver ADR-030 — e AddPlatformApi
// só cuida de Swagger/OpenAPI. AddPlatformInfrastructure já registra Serilog, OpenTelemetry
// e os health checks de dependências críticas (ver ADR-036).
// Os consumers de Realtime do Atendimento entram no MESMO bus do MassTransit (só pode haver um).
builder.Services.AddPlatformInfrastructure(
    builder.Configuration, builder.Environment, bus => bus.AddAtendimentoConsumers());
builder.Services.AddPlatformAuth(builder.Configuration);
builder.Services.AddPlatformApplication(builder.Configuration);
builder.Services.AddPlatformApi(builder.Configuration);
builder.Services.AddAdvocaciaRateLimiting();
builder.Services.AddAdvocaciaForwardedHeaders(builder.Configuration);

// Swagger UI (só em Development) e o dashboard do Hangfire são HTML com script inline: ficam
// com uma CSP mais frouxa. Toda a API JSON responde com default-src 'none' (ver ADR-067).
builder.Services.AddAdvocaciaSecurityHeaders(
    builder.Environment.IsDevelopment()
        ? ["/swagger", builder.Configuration["Hangfire:DashboardPath"] ?? "/jobs"]
        : [builder.Configuration["Hangfire:DashboardPath"] ?? "/jobs"]);

// Não anuncia "Server: Kestrel" (a SecurityHeadersMiddleware cobre o X-Powered-By).
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Módulos de negócio. AddCnjIntegration registra o ICnjClient consumido pelo job de captura
// do Legal — Legal.Infrastructure depende só da porta (Integrations.CNJ.Application), então
// a implementação é registrada aqui, pelo host (ver Legal.Infrastructure/DependencyInjection.cs).
builder.Services.AddCnjIntegration(builder.Configuration);
builder.Services.AddAnthropicIntegration(builder.Configuration);
builder.Services.AddLegalInfrastructure(builder.Configuration);
builder.Services.AddCrmInfrastructure(builder.Configuration);
builder.Services.AddWorkflowInfrastructure(builder.Configuration);
builder.Services.AddAtendimentoInfrastructure(builder.Configuration);

// Esquema de autenticação por chave de API do Bot (opt-in nos endpoints /bot/atendimentos).
// Depois de AddPlatformAuth: o JWT do Supabase continua sendo o esquema padrão.
builder.Services.AddAtendimentoApi();

var app = builder.Build();

// Primeiro de tudo: restaura IP/esquema reais do cliente a partir do X-Forwarded-* do proxy
// (Front Door/ingress). Rate limit por IP, HttpsRedirection e logs dependem disto.
app.UseForwardedHeaders();

// Headers de segurança em TODA resposta, inclusive as de middlewares que encerram o pipeline.
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(Advocacia.Platform.Api.Extensions.ServiceCollectionExtensions.CorsPolicyName);

// Ordem do pipeline (ver ADR-039/docs/api/conventions.md):
// 1. CorrelationId — precisa vir antes de tudo: até uma exceção não tratada deve responder
//    com o mesmo X-Correlation-Id que o cliente enviou (ou o gerado), e todo log da
//    requisição (inclusive os de ExceptionHandlingMiddleware) precisa do CorrelationId já
//    no contexto ambiente.
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. ExceptionHandling — captura qualquer exceção das etapas seguintes do pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 3. RequestLogging — loga a requisição já com CorrelationId disponível.
app.UseMiddleware<RequestLoggingMiddleware>();

// 4. Authentication — nativo do ASP.NET Core. Vem ANTES do rate limiter de propósito: o
//    limite de escrita/leitura é por usuário (claim "sub"), e antes de Authentication
//    HttpContext.User está vazio — o limite degeneraria para por-IP.
app.UseAuthentication();

// 5. RateLimiting — nativo do ASP.NET Core (ver RateLimitingConfiguration).
app.UseRateLimiter();

// 6. TenantContext — precisa vir depois de Authentication (lê claims do JWT via
//    ICurrentUser) e antes de Authorization (algumas policies dependem do tenant já
//    resolvido).
app.UseMiddleware<TenantContextMiddleware>();

// 7. Authorization — nativo do ASP.NET Core.
app.UseAuthorization();

// 8. Idempotency — roda só nos POSTs críticos, depois de Authorization (a chave de
//    idempotência é avaliada com tenant/usuário já resolvidos).
app.UseMiddleware<IdempotencyMiddleware>();

// 9. Endpoint — nativo do ASP.NET Core. Cada módulo de negócio registra os seus próprios
//    endpoints; o Platform não os conhece (ver ADR-001 e ModuleBoundaryTests).
app.MapPlatformEndpoints();
app.MapClientesEndpoints();
app.MapProcessosEndpoints();
app.MapPublicationsEndpoints();
app.MapContactsEndpoints();
app.MapCalendarEndpoints();
app.MapAtendimentosEndpoints();
app.MapBotEndpoints();

var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;
if (hangfireOptions.DashboardEnabled)
{
    app.UseHangfireDashboard(hangfireOptions.DashboardPath, new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()],
    });
}

// /health/live: só confirma que o processo está de pé, sem checar dependências externas.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// /health/ready: checa as dependências externas (Postgres, Redis, outbox, MassTransit) usadas pela aplicação.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// /health: todos os checks registrados, com detalhes — formato JSON estruturado.
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteAsync });

// /metrics: métricas Prometheus (ver ADR-036) — só para operação interna, nunca público.
app.MapPrometheusScrapingEndpoint("/metrics").RequireAuthorization(Policies.AdminOnly);

app.Run();

// Torna a classe Program (gerada implicitamente para top-level statements) pública e
// acessível a partir de outro assembly, para uso com WebApplicationFactory<Program> nos
// testes de integração de auth/JWT/policies (ver Platform.IntegrationTests).
public partial class Program;
