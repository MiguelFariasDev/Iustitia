using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.CRM.Domain.Contacts;

/// <summary>
/// Pessoa de relacionamento do escritório que não é cliente (parte contrária, testemunha,
/// fornecedor). Documento, e-mail e telefone são dado pessoal (LGPD): nunca logar, nunca
/// colocar em evento de domínio.
///
/// Nunca é excluído de verdade — só desativado, para o histórico de quem foi parte ou
/// testemunha continuar consultável. Este agregado não levanta eventos de domínio: nenhum
/// consumidor os usa nesta etapa, e evento sem consumidor só custaria a tabela de outbox.
/// </summary>
public sealed partial class Contact : AggregateRoot<ContactId>, IAuditable, ISoftDeletable, IHasTenant
{
    public const int NameMaxLength = 200;

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome em minúsculas, DERIVADO de <see cref="Name"/>, para busca indexável (pg_trgm).</summary>
    public string NameSearch { get; private set; } = string.Empty;

    public ContactType Type { get; private set; }

    /// <summary>CPF (11) ou CNPJ (14), somente dígitos. Opcional: nem toda testemunha tem o documento conhecido.</summary>
    public string? Document { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Somente dígitos, com DDD.</summary>
    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Contact()
    {
        // EF Core.
    }

    private Contact(ContactId id, Guid tenantId, ContactType type)
        : base(id)
    {
        TenantId = tenantId;
        Type = type;
        IsActive = true;
    }

    public static Result<Contact> Create(
        Guid tenantId,
        ContactType type,
        string? name,
        string? document,
        string? email,
        string? phone,
        string? notes)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Contact>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        var contact = new Contact(ContactId.New(), tenantId, type);
        var applied = contact.Apply(name, document, email, phone, notes);

        return applied.IsFailure ? Result.Failure<Contact>(applied.Error) : Result.Success(contact);
    }

    /// <summary>Atualiza os dados cadastrais; o <see cref="Type"/> também é editável (uma testemunha pode virar fornecedor de perícia).</summary>
    public Result Update(
        ContactType type, string? name, string? document, string? email, string? phone, string? notes)
    {
        var previousType = Type;
        Type = type;

        var applied = Apply(name, document, email, phone, notes);
        if (applied.IsFailure)
        {
            Type = previousType;
        }

        return applied;
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_ALREADY_INACTIVE));
        }

        IsActive = false;
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_ALREADY_ACTIVE));
        }

        IsActive = true;
        return Result.Success();
    }

    public static string? MaskDocument(string? digits) => digits?.Length switch
    {
        11 => $"{digits[..3]}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits.Substring(9, 2)}",
        14 => $"{digits[..2]}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits.Substring(12, 2)}",
        _ => digits,
    };

    public static string? MaskPhone(string? digits) => digits?.Length switch
    {
        11 => $"({digits[..2]}) {digits.Substring(2, 5)}-{digits.Substring(7, 4)}",
        10 => $"({digits[..2]}) {digits.Substring(2, 4)}-{digits.Substring(6, 4)}",
        _ => digits,
    };

    private Result Apply(string? name, string? document, string? email, string? phone, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_NAME_REQUIRED));
        }

        string? documentDigits = null;
        if (!string.IsNullOrWhiteSpace(document))
        {
            documentDigits = new string(document.Where(char.IsDigit).ToArray());
            if (documentDigits.Length is not (11 or 14))
            {
                return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_DOCUMENT_INVALID));
            }
        }

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            normalizedEmail = email.Trim().ToLowerInvariant();
            if (!EmailFormatRegex().IsMatch(normalizedEmail))
            {
                return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_EMAIL_INVALID));
            }
        }

        string? phoneDigits = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            phoneDigits = new string(phone.Where(char.IsDigit).ToArray());
            if (phoneDigits.Length is not (10 or 11))
            {
                return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_PHONE_INVALID));
            }
        }

        Name = name.Trim();
        NameSearch = Name.ToLowerInvariant();
        Document = documentDigits;
        Email = normalizedEmail;
        Phone = phoneDigits;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        return Result.Success();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormatRegex();
}
