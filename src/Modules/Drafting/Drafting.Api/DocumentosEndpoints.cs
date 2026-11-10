using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Drafting.Api;

/// <summary>
/// Acervo de documentos que alimenta o RAG. O upload extrai o texto na hora (CPU) e ENFILEIRA a
/// indexação: ela não bloqueia a criação de peças nem exige a GPU ligada no momento do envio.
/// </summary>
public static class DocumentosEndpoints
{
    /// <summary>Folga para os campos do formulário além do arquivo (o limite do arquivo é checado no serviço).</summary>
    private const long FormOverheadBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapDocumentosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/documentos", "Documentos");

        group.MapPost("/upload", async Task<IResult> (
                HttpRequest http, IDocumentIngestionService service, Microsoft.Extensions.Options.IOptions<Application.Services.IngestaoOptions> options,
                CancellationToken cancellationToken) =>
            {
                // Teto do corpo da requisição ANTES de ler o formulário: um arquivo gigante não chega a ser bufferizado.
                var maxBody = http.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (maxBody is { IsReadOnly: false })
                {
                    maxBody.MaxRequestBodySize = (options.Value.MaxMegabytes * 1024L * 1024L) + FormOverheadBytes;
                }

                if (!http.HasFormContentType)
                {
                    return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Envie multipart/form-data.");
                }

                IFormCollection form;
                try
                {
                    form = await http.ReadFormAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
                {
                    return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, options.Value.MaxMegabytes)).ToHttpResult();
                }

                var file = form.Files.GetFile("arquivo");
                if (file is null || file.Length == 0)
                {
                    return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "arquivo")).ToHttpResult();
                }

                var metadados = new UploadDocumentoRequest(
                    form["tipoDocumento"].ToString(),
                    NullIfEmpty(form["area"]),
                    NullIfEmpty(form["tribunal"]),
                    DateOnly.TryParse(form["dataPublicacao"], out var data) ? data : null,
                    NullIfEmpty(form["urlOrigem"]),
                    form["tags"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    NullIfEmpty(form["visibilidade"]));

                await using var stream = file.OpenReadStream();
                var result = await service.EnviarAsync(metadados, stream, file.FileName, file.Length, cancellationToken);

                return result.ToHttpResult(value => Results.Accepted($"/api/v1/documentos/{value.Id}", value));
            })
            // Autenticação por JWT (cabeçalho Authorization): não há cookie, então CSRF/antiforgery não se aplica.
            .DisableAntiforgery()
            .WithName("UploadDocumento")
            .WithSummary("Envia um documento (PDF com texto, DOCX ou TXT) ao acervo. Responde 202: a indexação é assíncrona.")
            .WithDescription(
                "multipart/form-data: arquivo (obrigatório), tipoDocumento (lei, jurisprudencia, sumula, doutrina, contrato, " +
                "peticao, sentenca, acordao, parecer, outro), area, tribunal, dataPublicacao (AAAA-MM-DD), urlOrigem, tags " +
                "(separadas por vírgula) e visibilidade (escritorio|privado; padrão escritorio). PDF escaneado é recusado (sem OCR).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async (int? pagina, int? tamanho, IDocumentIngestionService service, CancellationToken cancellationToken) =>
                (await service.ListarAsync(pagina ?? 1, tamanho ?? 20, cancellationToken)).ToHttpResult())
            .WithName("ListarDocumentosAcervo")
            .WithSummary("Documentos do acervo visíveis a você (os do escritório e os seus privados).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, IDocumentIngestionService service, CancellationToken cancellationToken) =>
                (await service.ObterAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ObterDocumentoAcervo")
            .WithSummary("Metadados e situação da indexação de um documento.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}", async (Guid id, IDocumentIngestionService service, CancellationToken cancellationToken) =>
                (await service.RemoverAsync(id, cancellationToken)).ToHttpResult())
            .WithName("RemoverDocumentoAcervo")
            .WithSummary("Remove o documento do acervo e do índice vetorial.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues value)
    {
        var text = value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
