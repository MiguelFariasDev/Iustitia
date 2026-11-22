using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Api;

/// <summary>
/// Casos e seus anexos. O caso é a unidade de isolamento: cada rota passa pela autorização do caso, e quem
/// não pode ver recebe 404 (nunca 403), para não revelar que ele existe.
/// </summary>
public static class CasosEndpoints
{
    /// <summary>Folga para os campos do formulário além do arquivo (o limite do arquivo é checado no serviço).</summary>
    private const long FormOverheadBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapCasosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/casos", "Casos");

        group.MapPost("/", async (CriarCasoRequest request, ICasoService service, CancellationToken cancellationToken) =>
                (await service.CriarAsync(request, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/casos/{value.Id}", value)))
            .WithName("CriarCaso")
            .WithSummary("Abre um caso. Quem cria é responsável por ele.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async (Guid? clienteId, int? pagina, int? tamanho, ICasoService service, CancellationToken cancellationToken) =>
                (await service.ListarAsync(clienteId, pagina ?? 1, tamanho ?? 20, cancellationToken)).ToHttpResult())
            .WithName("ListarCasos")
            .WithSummary("Casos a que você tem acesso (sócios e titulares veem os do escritório).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ICasoService service, CancellationToken cancellationToken) =>
                (await service.ObterAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ObterCaso")
            .WithSummary("Dados do caso e quem tem acesso.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}", async (Guid id, AtualizarCasoRequest request, ICasoService service, CancellationToken cancellationToken) =>
                (await service.AtualizarAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("AtualizarCaso")
            .WithSummary("Atualiza, encerra (\"encerrado\": true) ou reabre (\"encerrado\": false) o caso.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}/membros", async (Guid id, ConcederAcessoRequest request, ICasoService service, CancellationToken cancellationToken) =>
                (await service.ConcederAcessoAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("ConcederAcessoAoCaso")
            .WithSummary("Autoriza um usuário do escritório no caso (leitura ou escrita). Só responsável ou gestão.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}/membros/{usuarioId:guid}", async (Guid id, Guid usuarioId, ICasoService service, CancellationToken cancellationToken) =>
                (await service.RevogarAcessoAsync(id, usuarioId, cancellationToken)).ToHttpResult())
            .WithName("RevogarAcessoAoCaso")
            .WithSummary("Revoga o acesso de um usuário ao caso.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        MapAnexos(group);

        return app;
    }

    private static void MapAnexos(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/documentos", async Task<IResult> (
                Guid id, HttpRequest http, IAnexoService service, IOptions<AnexosOptions> options, CancellationToken cancellationToken) =>
            {
                // Teto do corpo ANTES de ler o formulário: um arquivo gigante não chega a ser bufferizado.
                var maxMb = Math.Max(options.Value.MaxMegabytes, options.Value.MaxMegabytesAudio);
                var limite = (maxMb * 1024L * 1024L) + FormOverheadBytes;
                var maxBody = http.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (maxBody is { IsReadOnly: false })
                {
                    maxBody.MaxRequestBodySize = limite;
                }

                if (!http.HasFormContentType)
                {
                    return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Envie multipart/form-data.");
                }

                IFormCollection form;
                try
                {
                    form = await http.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = limite }, cancellationToken);
                }
                catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
                {
                    return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, maxMb)).ToHttpResult();
                }

                var file = form.Files.GetFile("arquivo");
                if (file is null || file.Length == 0)
                {
                    return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "arquivo")).ToHttpResult();
                }

                var restrito = bool.TryParse(form["restrito"], out var valor) && valor;
                await using var stream = file.OpenReadStream();
                var result = await service.EnviarAsync(id, stream, file.FileName, file.Length, restrito, cancellationToken);

                return result.ToHttpResult(value => Results.Accepted($"/api/v1/casos/{id}/documentos/{value.Id}", value));
            })
            // Autenticação por JWT (cabeçalho Authorization): não há cookie, então CSRF/antiforgery não se aplica.
            .DisableAntiforgery()
            .WithName("AnexarDocumento")
            .WithSummary("Anexa um documento ao caso. Responde 202: leitura, OCR e indexação são assíncronos.")
            .WithDescription(
                "multipart/form-data: arquivo (obrigatório) e restrito (true = só você e o responsável pelo caso veem). " +
                "Aceita PDF (com ou sem texto), DOCX, DOC, TXT, MD, imagens, XLSX/XLS/ODS/CSV, e-mail (.eml) e áudio (se houver transcrição configurada).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/documentos", async (Guid id, IAnexoService service, CancellationToken cancellationToken) =>
                (await service.ListarAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ListarDocumentosDoCaso")
            .WithSummary("Anexos do caso: situação, confiança da leitura e classificação assistida.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/documentos/{documentoId:guid}", async (Guid id, Guid documentoId, IAnexoService service, CancellationToken cancellationToken) =>
                (await service.ObterAsync(id, documentoId, cancellationToken)).ToHttpResult())
            .WithName("ObterDocumentoDoCaso")
            .WithSummary("Um anexo do caso.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/documentos/{documentoId:guid}/original", async Task<IResult> (
                Guid id, Guid documentoId, HttpContext http, IAnexoService service, CancellationToken cancellationToken) =>
                Arquivo(http, await service.BaixarOriginalAsync(id, documentoId, cancellationToken)))
            .WithName("BaixarOriginal")
            .WithSummary("O arquivo original, sempre como download.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/documentos/{documentoId:guid}/markdown", async Task<IResult> (
                Guid id, Guid documentoId, HttpContext http, IAnexoService service, CancellationToken cancellationToken) =>
                Arquivo(http, await service.ObterMarkdownAsync(id, documentoId, cancellationToken)))
            .WithName("ObterMarkdown")
            .WithSummary("O texto extraído, para conferência contra o original.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/documentos/{documentoId:guid}/revisar", async (Guid id, Guid documentoId, IAnexoService service, CancellationToken cancellationToken) =>
                (await service.ConfirmarRevisaoAsync(id, documentoId, cancellationToken)).ToHttpResult())
            .WithName("ConfirmarRevisaoDoDocumento")
            .WithSummary("Confirma que uma pessoa conferiu o texto extraído (OCR, transcrição ou conteúdo suspeito).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/documentos/{documentoId:guid}/reprocessar", async (Guid id, Guid documentoId, IAnexoService service, CancellationToken cancellationToken) =>
                (await service.ReprocessarAsync(id, documentoId, cancellationToken))
                    .ToHttpResult(value => Results.Accepted($"/api/v1/casos/{id}/documentos/{documentoId}", value)))
            .WithName("ReprocessarDocumento")
            .WithSummary("Lê e indexa o documento de novo (após falha ou mudança de configuração).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}/documentos/{documentoId:guid}", async (Guid id, Guid documentoId, IAnexoService service, CancellationToken cancellationToken) =>
                (await service.RemoverAsync(id, documentoId, cancellationToken)).ToHttpResult())
            .WithName("RemoverDocumentoDoCaso")
            .WithSummary("Apaga o documento do índice, do armazenamento e do banco.")
            .RequireAuthorization(Policies.LawyerOrAbove);
    }

    private static IResult Arquivo(HttpContext http, Result<AnexoConteudo> resultado)
    {
        if (resultado.IsFailure)
        {
            return resultado.ToHttpResult();
        }

        // Conteúdo enviado por terceiros: nunca renderizar nem adivinhar o tipo na origem da API, e nunca em cache.
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        http.Response.Headers.CacheControl = "no-store";

        return Results.File(resultado.Value.Conteudo, resultado.Value.ContentType, resultado.Value.NomeArquivo);
    }
}
