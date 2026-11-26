using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Application.Abstractions;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence;

/// <summary><see cref="IWorkflowUnitOfWork"/> sobre o <see cref="WorkflowDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class WorkflowUnitOfWork(WorkflowDbContext dbContext) : UnitOfWorkBase<WorkflowDbContext>(dbContext), IWorkflowUnitOfWork;
