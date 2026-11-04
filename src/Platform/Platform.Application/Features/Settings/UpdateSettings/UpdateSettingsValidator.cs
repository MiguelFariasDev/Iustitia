using System.Text.Json;
using FluentValidation;

namespace Advocacia.Platform.Application.Features.Settings.UpdateSettings;

public sealed class UpdateSettingsValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ValueJson).NotEmpty().Must(BeValidJson).WithMessage("O valor precisa ser um JSON válido.");
    }

    private static bool BeValidJson(string valueJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(valueJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
