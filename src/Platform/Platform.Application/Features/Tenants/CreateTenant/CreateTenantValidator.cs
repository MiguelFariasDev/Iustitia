using Advocacia.Platform.Domain.Tenancy;
using FluentValidation;

namespace Advocacia.Platform.Application.Features.Tenants.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountType)
            .Must(type => Enum.TryParse<AccountType>(type, ignoreCase: true, out _))
            .WithMessage("Tipo de conta inválido. Valores aceitos: Firm, Solo.");

        // Cada tipo exige o seu documento; a validade dos dígitos fica nos value objects.
        // As mensagens nunca ecoam o documento: CPF/CNPJ é dado pessoal.
        When(x => IsSolo(x.AccountType), () => RuleFor(x => x.Cpf).NotEmpty().WithMessage("O CPF é obrigatório para conta de advogado(a) autônomo(a)."));
        When(x => !IsSolo(x.AccountType), () => RuleFor(x => x.Cnpj).NotEmpty().WithMessage("O CNPJ do escritório é obrigatório."));
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.AdminPhone).MaximumLength(20);
        RuleFor(x => x.AdminOabNumber).MaximumLength(20);
        RuleFor(x => x.AdminOabState).MaximumLength(2);
    }

    private static bool IsSolo(string accountType) =>
        Enum.TryParse<AccountType>(accountType, ignoreCase: true, out var parsed) && parsed == AccountType.Solo;
}
