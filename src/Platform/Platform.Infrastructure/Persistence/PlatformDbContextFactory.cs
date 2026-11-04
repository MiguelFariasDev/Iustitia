using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Platform.Infrastructure.Persistence;

/// <summary>
/// Permite `dotnet ef migrations`/`dotnet ef database update` funcionarem em design-time
/// sem depender do composition root real (Program.cs) — só o suficiente para construir o
/// modelo, sem interceptors, DI ou configuração de app.
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        // Design-time (migrations) nunca filtra por tenant; um AsyncLocalTenantContext
        // "vazio" satisfaz o construtor sem afetar o modelo gerado.
        return new PlatformDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
