using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence;

/// <summary>Permite `dotnet ef migrations` sem o composition root real — mesmo padrão de LegalDbContextFactory.</summary>
public sealed class AtendimentoDbContextFactory : IDesignTimeDbContextFactory<AtendimentoDbContext>
{
    public AtendimentoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADVOCACIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AtendimentoDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AtendimentoDbContext(optionsBuilder.Options, new AsyncLocalTenantContext());
    }
}
