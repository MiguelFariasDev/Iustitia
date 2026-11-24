namespace Advocacia.Modules.Drafting.Domain.Normas;

/// <summary>
/// Metadados já validados de um Markdown do acervo: o que o YAML trouxe, completado (quando faltava)
/// por caminho e conteúdo. Dado puro — quem o produz é o <c>NormaMetadataResolver</c>.
/// </summary>
public sealed record NormaMetadata(
    string Titulo,
    string Tipo,
    string? Numero,
    int? Ano,
    DateOnly? Data,
    string? DataPublicacao,
    string? Ementa,
    string Situacao,
    bool Revogada,
    string VersaoTexto,
    string? Urn,
    string FonteOficialUrl,
    string Esfera,
    string? Uf,
    string? Municipio,
    string? Tribunal,
    string? Area,
    IReadOnlyList<string> Assuntos,
    string Colecao);
