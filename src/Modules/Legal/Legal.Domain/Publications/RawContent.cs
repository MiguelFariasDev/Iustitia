using System.Net;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Inteiro teor da publicação, normalizado: sem marcação HTML, com entidades decodificadas
/// e espaços em branco colapsados. O DJEN devolve o texto com resquícios de HTML
/// (&lt;p&gt;, &lt;br&gt;, &amp;nbsp;) que variam por tribunal — normalizar na entrada é o
/// que torna a busca full-text e a deduplicação por conteúdo previsíveis.
///
/// ATENÇÃO: este valor pode conter dados pessoais e sigilosos de partes do processo.
/// NUNCA logar, nunca enviar para telemetria, nunca incluir em mensagem de erro
/// (ver claude.md, seção 14 — sigilo profissional e LGPD).
/// </summary>
public sealed partial class RawContent : ValueObject
{
    /// <summary>Limite defensivo: publicações do DJEN raramente passam de ~50k caracteres.</summary>
    public const int MaxLength = 1_000_000;

    public string Value { get; }

    private RawContent(string value)
    {
        Value = value;
    }

    public static Result<RawContent> Create(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure<RawContent>(ErrorFactory.From(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        }

        var normalized = Normalize(content);

        if (normalized.Length == 0)
        {
            return Result.Failure<RawContent>(ErrorFactory.From(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        }

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<RawContent>(
                ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(RawContent)));
        }

        return Result.Success(new RawContent(normalized));
    }

    /// <summary>
    /// Reconstrói o value object a partir de um valor já normalizado vindo do banco, sem
    /// revalidar. Usado só pela conversão do EF Core (ver PublicationConfiguration): passar
    /// pelo <see cref="Create"/> na materialização re-normalizaria a cada leitura e poderia
    /// lançar em cima de dado legado, além do custo de regex em toda query.
    /// </summary>
    public static RawContent FromPersistence(string value) => new(value);

    private static string Normalize(string content)
    {
        var withoutHtml = HtmlTagRegex().Replace(content, " ");
        var decoded = WebUtility.HtmlDecode(withoutHtml);

        //   (NBSP) sobra da decodificação de &nbsp; e não é capturado por \s em .NET
        // no modo padrão de algumas culturas — trocar explicitamente antes de colapsar.
        var withoutNbsp = decoded.Replace(' ', ' ');

        return WhitespaceRegex().Replace(withoutNbsp, " ").Trim();
    }

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Deliberadamente NÃO devolve o conteúdo: <c>ToString()</c> é o caminho mais fácil
    /// para o texto vazar num log estruturado ou numa mensagem de exceção. Quem precisa do
    /// texto usa <see cref="Value"/> explicitamente.
    /// </summary>
    public override string ToString() => $"RawContent({Value.Length} caracteres)";
}
