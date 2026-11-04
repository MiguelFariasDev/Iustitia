using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.CRM.Application.Abstractions;

/// <summary>Fronteira transacional do módulo CRM (um DbContext por módulo — ver ILegalUnitOfWork).</summary>
public interface ICrmUnitOfWork : IUnitOfWork;
