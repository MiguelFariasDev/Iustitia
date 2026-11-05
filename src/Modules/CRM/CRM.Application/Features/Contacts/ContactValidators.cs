using Advocacia.Modules.CRM.Domain.Contacts;
using FluentValidation;

namespace Advocacia.Modules.CRM.Application.Features.Contacts;

/// <summary>Validação de FORMA; regras de domínio ficam no agregado. Mensagens nunca ecoam documento/e-mail/telefone.</summary>
public sealed class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactValidator()
    {
        RuleFor(command => command.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<ContactType>(type, ignoreCase: true, out _))
            .WithMessage("Tipo de contato inválido. Valores aceitos: ParteContraria, Testemunha, Fornecedor, Outros.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Contact.NameMaxLength);
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator()
    {
        RuleFor(command => command.ContactId).NotEmpty();
        RuleFor(command => command.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<ContactType>(type, ignoreCase: true, out _))
            .WithMessage("Tipo de contato inválido. Valores aceitos: ParteContraria, Testemunha, Fornecedor, Outros.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Contact.NameMaxLength);
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}
