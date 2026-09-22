using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.BuildingBlocks.Infrastructure.Identity;

/// <summary>
/// Implementação padrão de <see cref="ICurrentUser"/> usada fora de um contexto HTTP
/// autenticado (Worker, migrations, testes). Sempre reporta "não autenticado".
///
/// A implementação real, lendo claims do JWT validado do Supabase, é registrada por
/// Platform.Infrastructure na Etapa 0.3 e substitui esta no container de DI dos hosts
/// web. Esta classe garante que AuditInterceptor e afins tenham sempre uma
/// implementação válida para depender, mesmo antes da Etapa 0.3.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;

    public Guid? TenantId => null;

    public string? Email => null;

    public IReadOnlyCollection<string> Roles { get; } = [];

    public bool IsInRole(string role) => false;
}
