using Advocacia.Platform.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// TODO (Etapa 0.3): Serilog estruturado (console + Application Insights)
// TODO (Etapa 0.5): Hangfire (storage + dashboard) e agendamento do OutboxProcessor
// TODO (Fase 1+): registrar jobs recorrentes por módulo (CNJ polling, etc.)

builder.Services.AddPlatformInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
