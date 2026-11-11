using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Workflow.Domain.Tags;

/// <summary>Etiqueta do escritório (nome + cor), reutilizável em vários processos e advogados.</summary>
public sealed partial class Tag : AggregateRoot<TagId>, IAuditable, ISoftDeletable, IHasTenant
{
    public const int NameMaxLength = 30;
    public const string DefaultColor = "#64748b";

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Cor em hexadecimal (#RRGGBB).</summary>
    public string Color { get; private set; } = DefaultColor;

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Tag()
    {
        // EF Core.
    }

    private Tag(TagId id, Guid tenantId, string name, string color)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Color = color;
    }

    public static Result<Tag> Create(Guid tenantId, string? name, string? color)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Tag>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            return Result.Failure<Tag>(ErrorFactory.From(ErrorCode.TAG_NAME_REQUIRED));
        }

        var validColor = color is not null && HexColor().IsMatch(color) ? color.ToLowerInvariant() : DefaultColor;

        return Result.Success(new Tag(TagId.New(), tenantId, name.Trim(), validColor));
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
