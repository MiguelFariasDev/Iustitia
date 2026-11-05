using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Tenants.CreateTenant;

/// <summary>
/// Cadastro de conta. <c>AccountType</c> é "Firm" (escritório, identificado por CNPJ — o padrão,
/// e o único tipo que existia antes do ADR-068) ou "Solo" (advogado(a) autônomo(a),
/// identificado(a) por CPF). Em Solo, <c>Cnpj</c> é ignorado e <c>Name</c> leva o nome do titular.
/// </summary>
public sealed record CreateTenantCommand(
    string Name,
    string? Cnpj,
    string AdminName,
    string AdminEmail,
    string AdminPassword,
    string AccountType = "Firm",
    string? Cpf = null) : ICommand<CreateTenantResponse>;
