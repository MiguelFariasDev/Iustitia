using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;

/// <summary>
/// Permite `dotnet ef migrations`/`database update` sobre o AnthropicDbContext sem o composition
/// root real — mesmo padrão de LegalDbContextFactory.
/// </summary>
public sealed class AnthropicDbContextFactory : IDesignTimeDbContextFactory<AnthropicDbContext>
{
    public AnthropicDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AnthropicDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AnthropicDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
