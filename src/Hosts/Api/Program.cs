using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Legal.Api;
using Advocacia.Modules.Legal.Infrastructure;
using Advocacia.Modules.Workflow.Api;
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

// TODO (Fase 2+): registrar os módulos de negócio restantes (Documents, ...) conforme entram no roadmap

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
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddPlatformAuth(builder.Configuration);
builder.Services.AddPlatformApplication(builder.Configuration);
builder.Services.AddPlatformApi(builder.Configuration);
builder.Services.AddAdvocaciaRateLimiting();

// Módulos de negócio. AddCnjIntegration registra o ICnjClient consumido pelo job de captura
// do Legal — Legal.Infrastructure depende só da porta (Integrations.CNJ.Application), então
// a implementação é registrada aqui, pelo host (ver Legal.Infrastructure/DependencyInjection.cs).
builder.Services.AddCnjIntegration(builder.Configuration);
builder.Services.AddLegalInfrastructure(builder.Configuration);
builder.Services.AddWorkflowInfrastructure(builder.Configuration);

var app = builder.Build();

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

// 4. RateLimiting — nativo do ASP.NET Core (ver RateLimitingConfiguration).
app.UseRateLimiter();

// 5/7. Authentication/Authorization — nativos do ASP.NET Core.
app.UseAuthentication();

// 6. TenantContext — precisa vir depois de Authentication (lê claims do JWT via
//    ICurrentUser) e antes de Authorization (algumas policies dependem do tenant já
//    resolvido).
app.UseMiddleware<TenantContextMiddleware>();

// 6b. RevokedDevice — recusa (401 DEVICE_REVOKED) o dispositivo revogado que envia X-Device-Id.
//     Depois do TenantContext (precisa do tenant) e antes da Authorization (ADR-069).
app.UseMiddleware<RevokedDeviceMiddleware>();

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
app.MapWorkTasksEndpoints();
app.MapAgendaEndpoints();

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
