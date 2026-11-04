using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Tenancy.Events;

namespace Advocacia.Platform.Domain.Tenancy;

/// <summary>Escritório de advocacia cliente do sistema — raiz do isolamento multi-tenant.</summary>
public sealed class Tenant : AggregateRoot<TenantId>, IAuditable, ISoftDeletable
{
    public string Name { get; private set; } = null!;

    public CNPJ Cnpj { get; private set; } = null!;

    public TenantStatus Status { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? AddressJson { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Tenant()
    {
        // EF Core.
    }

    private Tenant(TenantId id, string name, CNPJ cnpj)
        : base(id)
    {
        Name = name;
        Cnpj = cnpj;
        Status = TenantStatus.Active;
    }

    public static Result<Tenant> Create(string name, CNPJ cnpj)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Tenant>(ErrorFactory.From(ErrorCode.TENANT_NAME_REQUIRED));
        }

        var tenant = new Tenant(TenantId.New(), name.Trim(), cnpj);
        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id));

        return Result.Success(tenant);
    }

    public Result Update(string name, string? logoUrl, string? addressJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TENANT_NAME_REQUIRED));
        }

        Name = name.Trim();
        LogoUrl = logoUrl;
        AddressJson = addressJson;

        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status == TenantStatus.Cancelled)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TENANT_CANCELLED));
        }

        if (Status == TenantStatus.Suspended)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TENANT_SUSPENDED));
        }

        Status = TenantStatus.Suspended;
        RaiseDomainEvent(new TenantSuspendedEvent(Id));

        return Result.Success();
    }

    public Result Reactivate()
    {
        if (Status != TenantStatus.Suspended)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        Status = TenantStatus.Active;

        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == TenantStatus.Cancelled)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TENANT_CANCELLED));
        }

        Status = TenantStatus.Cancelled;
        RaiseDomainEvent(new TenantCancelledEvent(Id));

        return Result.Success();
    }
}
