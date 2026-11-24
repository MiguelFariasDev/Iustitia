using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence;

/// <summary>Usada só pelo <c>dotnet ef</c> (design-time). Nunca vai para produção.</summary>
public sealed class DraftingDbContextFactory : IDesignTimeDbContextFactory<DraftingDbContext>
{
    public DraftingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<DraftingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new DraftingDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
