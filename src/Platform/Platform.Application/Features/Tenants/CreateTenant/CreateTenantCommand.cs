using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Tenants.CreateTenant;

public sealed record CreateTenantCommand(
    string Name,
    string Cnpj,
    string AdminName,
    string AdminEmail,
    string AdminPassword) : ICommand<CreateTenantResponse>;
