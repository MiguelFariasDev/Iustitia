namespace Advocacia.Modules.Drafting.Application.Models;

/// <summary>Metadados do upload (o arquivo vai à parte, em multipart).</summary>
public sealed record UploadDocumentoRequest(
    string TipoDocumento,
    string? Area = null,
    string? Tribunal = null,
    DateOnly? DataPublicacao = null,
    string? UrlOrigem = null,
    IReadOnlyList<string>? Tags = null,
    string? Visibilidade = null);

public sealed record UploadDocumentoResponse(Guid Id, string Status, string Mensagem);

public sealed record DocumentoResponse(
    Guid Id,
    string NomeArquivo,
    string TipoDocumento,
    string? Area,
    string? Tribunal,
    string Visibilidade,
    string Status,
    int Trechos,
    DateTimeOffset CriadoEm,
    DateTimeOffset? IndexadoEm);

public sealed record ExtractedPage(int? Numero, string Texto);
