using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence;

/// <summary>
/// Permite `dotnet ef migrations`/`dotnet ef database update` sobre o LegalDbContext sem
/// depender do composition root real — mesmo padrão de PlatformDbContextFactory.
/// </summary>
public sealed class LegalDbContextFactory : IDesignTimeDbContextFactory<LegalDbContext>
{
    public LegalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LegalDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        // Design-time nunca filtra por tenant (ver PlatformDbContextFactory).
        return new LegalDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
