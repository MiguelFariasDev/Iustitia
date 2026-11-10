using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

/// <summary>Fronteira transacional do módulo Workflow (um DbContext por módulo — ver ILegalUnitOfWork).</summary>
public interface IWorkflowUnitOfWork : IUnitOfWork;
