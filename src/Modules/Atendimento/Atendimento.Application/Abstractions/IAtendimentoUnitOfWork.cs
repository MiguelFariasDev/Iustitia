using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

/// <summary>
/// Fronteira transacional do módulo — mesmo motivo de ILegalUnitOfWork: o IUnitOfWork padrão
/// resolve para o PlatformDbContext, e um handler daqui que o usasse salvaria no contexto errado.
/// </summary>
public interface IAtendimentoUnitOfWork : IUnitOfWork;
