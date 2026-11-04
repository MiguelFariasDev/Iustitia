using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Fronteira transacional do módulo Legal. Existe como interface própria porque cada módulo
/// tem seu próprio DbContext sobre o mesmo banco físico (Modular Monolith — ver ADR-001):
/// o <see cref="IUnitOfWork"/> registrado por padrão resolve para o PlatformDbContext, e
/// um handler de Legal que o usasse salvaria no contexto errado.
///
/// Um único <c>SaveChangesAsync</c> do LegalDbContext já é atômico e grava, na mesma
/// transação, a publicação e as mensagens de outbox dos seus eventos de domínio
/// (OutboxInterceptor — ver ADR-008). Begin/Commit explícitos só são necessários quando um
/// handler coordena mais de uma escrita.
/// </summary>
public interface ILegalUnitOfWork : IUnitOfWork;
