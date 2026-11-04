using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.Modules.CRM.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Legal.Infrastructure;
using Advocacia.Modules.Legal.Infrastructure.Jobs.RecurringJobs;
using Advocacia.Modules.Workflow.Infrastructure;
using Advocacia.Modules.Workflow.Infrastructure.Jobs;
using Advocacia.Platform.Infrastructure;
using Advocacia.Platform.Infrastructure.Jobs.RecurringJobs;

// TODO (Fase 2+): registrar jobs recorrentes dos módulos restantes conforme entram no roadmap

var builder = Host.CreateApplicationBuilder(args);

// No-op em dev (sem KeyVault:Uri configurado) — em produção, resolve segredos via Managed
// Identity (ver ADR-036/docs/compliance/production-config.md).
builder.Configuration.AddAdvocaciaKeyVault();

// AddPlatformInfrastructure já registra Serilog e OpenTelemetry (ver ADR-036) — o Worker não
// expõe endpoints HTTP, mas os jobs e consumers continuam logando/instrumentados do mesmo
// jeito que a Api.
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);

// Módulos de negócio (ver comentário equivalente em Hosts/Api/Program.cs).
builder.Services.AddCnjIntegration(builder.Configuration);
builder.Services.AddLegalInfrastructure(builder.Configuration);
builder.Services.AddCrmInfrastructure(builder.Configuration);
builder.Services.AddWorkflowInfrastructure(builder.Configuration);

var host = builder.Build();

// Registra os jobs recorrentes (Hangfire) uma vez, após o host (e o storage do Hangfire)
// estarem prontos — ver RecurringJobsRegistrar.
// Cada módulo registra os seus próprios jobs; o Platform não conhece os jobs dos módulos
// (ver ADR-001).
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RecurringJobsRegistrar>().RegisterAll();
    scope.ServiceProvider.GetRequiredService<LegalRecurringJobsRegistrar>().RegisterAll();
    scope.ServiceProvider.GetRequiredService<WorkflowRecurringJobsRegistrar>().RegisterAll();
}

host.Run();
