using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Processos;

/// <summary>
/// Onde o processo tramita: tribunal, comarca e vara. Agrupados porque só fazem sentido
/// juntos e são preenchidos na mesma etapa do cadastro.
///
/// <see cref="Tribunal"/> é a sigla usada nas consultas ao DJEN (ex.: "TJSP"), por isso é
/// normalizada para maiúsculas — o CNJ diferencia caixa no parâmetro siglaTribunal.
/// </summary>
public sealed class DadosForo : ValueObject
{
    public static readonly DadosForo Vazio = new(null, null, null);

    public string? Tribunal { get; }

    public string? Comarca { get; }

    public string? Vara { get; }

    private DadosForo(string? tribunal, string? comarca, string? vara)
    {
        Tribunal = tribunal;
        Comarca = comarca;
        Vara = vara;
    }

    /// <summary>
    /// Nunca falha: os três campos são opcionais. O tribunal pode ser derivado do próprio
    /// número CNJ quando não informado, então exigi-lo no cadastro seria pedir ao usuário
    /// algo que o sistema já sabe.
    /// </summary>
    public static Result<DadosForo> Create(string? tribunal, string? comarca, string? vara) =>
        Result.Success(new DadosForo(
            Normalizar(tribunal)?.ToUpperInvariant(),
            Normalizar(comarca),
            Normalizar(vara)));

    public static DadosForo FromPersistence(string? tribunal, string? comarca, string? vara) =>
        new(tribunal, comarca, vara);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Tribunal;
        yield return Comarca;
        yield return Vara;
    }
}
