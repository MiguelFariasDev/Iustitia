using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Modules.CRM.Infrastructure.Persistence;

/// <summary>Permite `dotnet ef migrations` sobre o CrmDbContext sem o composition root real.</summary>
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new CrmDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
