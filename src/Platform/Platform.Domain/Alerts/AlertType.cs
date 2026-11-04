namespace Advocacia.Platform.Domain.Alerts;

/// <summary>Origem do alerta. Persistido como texto (nome do membro), então o nome não deve mudar.</summary>
public enum AlertType
{
    Publication,
    Task,
    Birthday,
    Other,
}
