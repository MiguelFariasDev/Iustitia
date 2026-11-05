using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.Modules.Atendimentos.Infrastructure;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Legal.Infrastructure;
using Advocacia.Modules.Legal.Infrastructure.Jobs;
using Advocacia.Modules.Workflow.Infrastructure;
using Advocacia.Modules.Legal.Infrastructure.Jobs.RecurringJobs;
using Advocacia.Platform.Infrastructure;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using Advocacia.Platform.Infrastructure.Jobs.RecurringJobs;

// TODO (Fase 2+): registrar jobs recorrentes dos módulos restantes conforme entram no roadmap

var builder = Host.CreateApplicationBuilder(args);

// No-op em dev (sem KeyVault:Uri configurado) — em produção, resolve segredos via Managed
// Identity (ver ADR-036/docs/compliance/production-config.md).
builder.Configuration.AddAdvocaciaKeyVault();

// AddPlatformInfrastructure já registra Serilog e OpenTelemetry (ver ADR-036) — o Worker não
// expõe endpoints HTTP, mas os jobs e consumers continuam logando/instrumentados do mesmo
// jeito que a Api.
// Consumers de Realtime do Atendimento no mesmo bus (ver Hosts/Api/Program.cs).
builder.Services.AddPlatformInfrastructure(
    builder.Configuration, builder.Environment, bus => bus.AddAtendimentoConsumers());

// Módulos de negócio (ver comentário equivalente em Hosts/Api/Program.cs).
builder.Services.AddCnjIntegration(builder.Configuration);
builder.Services.AddAnthropicIntegration(builder.Configuration);
builder.Services.AddWorkflowInfrastructure();
builder.Services.AddLegalInfrastructure(builder.Configuration);
builder.Services.AddAtendimentoInfrastructure(builder.Configuration);

var host = builder.Build();

// Modo utilitário (não sobe o worker): cifra dados legados / re-cifra após rotação de chave.
// Rodar com a connection string do DONO do schema. Ver docs/runbooks/field-encryption.md.
if (args.Contains("--backfill-field-encryption"))
{
    using var backfillScope = host.Services.CreateScope();

    // O host não foi iniciado (sem hosted services): resolve o cifrador à mão, o que também o
    // publica no acessor estático usado pelos conversores do EF Core.
    backfillScope.ServiceProvider.GetRequiredService<Advocacia.BuildingBlocks.Infrastructure.Security.IFieldEncryptor>();

    var clientes = await backfillScope.ServiceProvider.GetRequiredService<ClienteEncryptionBackfillJob>().RunAsync(CancellationToken.None);
    var usuarios = await backfillScope.ServiceProvider.GetRequiredService<UserEncryptionBackfillJob>().RunAsync(CancellationToken.None);
    Console.WriteLine($"Backfill concluído: {clientes} clientes, {usuarios} usuários.");
    return;
}

// Registra os jobs recorrentes (Hangfire) uma vez, após o host (e o storage do Hangfire)
// estarem prontos — ver RecurringJobsRegistrar.
// Cada módulo registra os seus próprios jobs; o Platform não conhece os jobs dos módulos
// (ver ADR-001).
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RecurringJobsRegistrar>().RegisterAll();
    scope.ServiceProvider.GetRequiredService<LegalRecurringJobsRegistrar>().RegisterAll();
}

host.Run();
