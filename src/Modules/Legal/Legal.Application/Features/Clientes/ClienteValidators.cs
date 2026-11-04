using Advocacia.Modules.Legal.Domain.Clientes;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.Clientes;

/// <summary>
/// Validação de FORMA. O que é regra de domínio (dígitos verificadores do CPF/CNPJ,
/// coerência entre tipo e documento) fica nos value objects e é tratado pelo handler — aqui
/// só o barato, para falhar antes de tocar o banco (ver ADR-031).
///
/// As mensagens nunca ecoam o documento: CPF/CNPJ é dado pessoal e a mensagem de validação
/// vai para o corpo da resposta e para os logs.
/// </summary>
public sealed class CreateClienteValidator : AbstractValidator<CreateClienteCommand>
{
    public CreateClienteValidator()
    {
        RuleFor(command => command.Tipo)
            .NotEmpty()
            .Must(tipo => Enum.TryParse<TipoCliente>(tipo, ignoreCase: true, out _))
            .WithMessage("Tipo de cliente inválido. Valores aceitos: PessoaFisica, PessoaJuridica.");

        RuleFor(command => command.Nome).NotEmpty().MaximumLength(NomeCliente.MaxLength);

        RuleFor(command => command.Documento)
            .NotEmpty()
            .WithMessage("O CPF ou CNPJ do cliente é obrigatório.");

        RuleFor(command => command.Observacoes).MaximumLength(2000);
    }
}

public sealed class UpdateClienteValidator : AbstractValidator<UpdateClienteCommand>
{
    public UpdateClienteValidator()
    {
        RuleFor(command => command.ClienteId).NotEmpty();
        RuleFor(command => command.Nome).NotEmpty().MaximumLength(NomeCliente.MaxLength);
        RuleFor(command => command.Observacoes).MaximumLength(2000);
    }
}
