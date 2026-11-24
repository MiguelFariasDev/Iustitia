using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Financeiro.Application.Abstractions;

/// <summary>Fronteira transacional do módulo Financeiro (um DbContext por módulo — ver ILegalUnitOfWork).</summary>
public interface IFinanceiroUnitOfWork : IUnitOfWork;
