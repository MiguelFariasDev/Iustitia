using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.CapturePublication;

/// <summary>
/// Validações de forma da requisição. O que é regra de domínio (formato CNJ com dígitos
/// verificadores, data no futuro, normalização do conteúdo) fica nos value objects e é
/// tratado pelo handler — aqui só o que é barato e puramente estrutural, para falhar antes
/// de tocar o banco (ver ADR-031).
/// </summary>
public sealed class CapturePublicationValidator : AbstractValidator<CapturePublicationCommand>
{
    public CapturePublicationValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.ExternalId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.CnjNumber).NotEmpty().MaximumLength(30);

        // Mensagem sem interpolar o conteúdo: RawContent pode conter dados sigilosos e a
        // mensagem de validação vai para o corpo da resposta e para os logs.
        RuleFor(command => command.RawContent).NotEmpty().WithMessage("O conteúdo da publicação é obrigatório.");

        RuleFor(command => command.PublishedAt).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.Source).IsInEnum();
    }
}
