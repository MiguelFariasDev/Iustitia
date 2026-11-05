using Advocacia.Modules.Legal.Application.Abstractions;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>Implementação de escopo (um por execução do job) de <see cref="IAiUsageContext"/>.</summary>
public sealed class AiUsageContext : IAiUsageContext
{
    public Guid? PublicationId { get; set; }
}
