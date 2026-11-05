using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

/// <summary>
/// Fronteira transacional do módulo Workflow. Interface própria pelo mesmo motivo de
/// <c>ILegalUnitOfWork</c>: cada módulo tem seu DbContext sobre o mesmo banco, e o
/// <see cref="IUnitOfWork"/> padrão resolve para o PlatformDbContext — um handler daqui que o usasse
/// salvaria no contexto errado, sem erro visível.
/// </summary>
public interface IWorkflowUnitOfWork : IUnitOfWork;
