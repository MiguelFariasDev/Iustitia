using System.Text;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Casos;

/// <summary>
/// Anexos de um caso. O upload só valida, cifra, guarda o original e ENFILEIRA: conversão e OCR rodam no
/// worker com CPU; classificação e embeddings, quando a GPU estiver ligada.
/// </summary>
public sealed class AnexoService(
    IOptions<DraftingOptions> draftingOptions,
    IOptions<AnexosOptions> anexosOptions,
    ICasoAcessoService acesso,
    ICasoDocumentoRepository documentos,
    ICasoArquivoStore arquivos,
    ICasoVectorStore vetores,
    IGeracaoPecaFila fila,
    IDraftingUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<AnexoService> logger) : IAnexoService
{
    public async Task<Result<UploadAnexoResponse>> EnviarAsync(
        Guid casoId, Stream conteudo, string nomeArquivo, long tamanhoDeclarado, bool restrito,
        CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Falha<UploadAnexoResponse>(ErrorCode.DRAFTING_DISABLED);
        }

        var autorizado = await acesso.AutorizarAsync(casoId, escrita: true, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<UploadAnexoResponse>(autorizado.Error);
        }

        var nome = NomeSeguro(nomeArquivo);
        if (nome.Length == 0)
        {
            return Result.Failure<UploadAnexoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "arquivo"));
        }

        var limites = anexosOptions.Value;
        var maxMb = Math.Max(limites.MaxMegabytes, limites.MaxMegabytesAudio);
        if (tamanhoDeclarado > maxMb * 1024L * 1024L)
        {
            return Result.Failure<UploadAnexoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, maxMb));
        }

        // O tamanho declarado pode mentir: o que vale é o que foi lido, e a leitura para no limite.
        var bytes = await LerAteAsync(conteudo, maxMb * 1024L * 1024L, cancellationToken);
        if (bytes is null)
        {
            return Result.Failure<UploadAnexoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, maxMb));
        }

        if (DetectorFormato.Detectar(bytes, nome) is not { } formato)
        {
            return Falha<UploadAnexoResponse>(ErrorCode.DRAFTING_UNSUPPORTED_FILE);
        }

        var limiteDoFormato = formato == FormatoDocumento.Audio ? limites.MaxMegabytesAudio : limites.MaxMegabytes;
        if (bytes.LongLength > limiteDoFormato * 1024L * 1024L)
        {
            return Result.Failure<UploadAnexoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, limiteDoFormato));
        }

        var caso = autorizado.Value.Caso;
        var existentes = await documentos.ListarAsync(caso.Id, cancellationToken);
        if (existentes.Count >= limites.MaxDocumentosPorCaso)
        {
            return Result.Failure<UploadAnexoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_TOO_MANY_DOCUMENTS, limites.MaxDocumentosPorCaso));
        }

        var hash = TextoHash.DosBytes(bytes);
        if (await documentos.ObterPorHashAsync(caso.Id, hash, cancellationToken) is not null)
        {
            return Falha<UploadAnexoResponse>(ErrorCode.DRAFTING_DOCUMENT_DUPLICATE);
        }

        // A chave não tem nome de arquivo nem de cliente: só ids. O conteúdo vai cifrado.
        var id = CasoDocumentoId.New();
        var chave = $"casos/{caso.TenantId:N}/{caso.Id.Value:N}/{id.Value:N}/original";
        try
        {
            await arquivos.GravarAsync(chave, bytes, cancellationToken);
        }
        catch (StorageException ex)
        {
            logger.LogError(ex, "Anexos: falha ao guardar o original.");

            return Falha<UploadAnexoResponse>(ErrorCode.DRAFTING_STORAGE_UNAVAILABLE);
        }

        var documento = CasoDocumento.Criar(
            id, caso, autorizado.Value.UsuarioId, nome, formato, bytes.LongLength, hash, chave, restrito, clock.UtcNow);

        // Metadados e item da fila no mesmo SaveChanges: nunca existe anexo sem processamento pendente.
        documentos.Add(documento);
        fila.Enfileirar(caso.TenantId, QueueItemKind.Conversao, id.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UploadAnexoResponse(
            id.Value, documento.Status.ToString(), "Documento recebido. Ele será lido e indexado em segundo plano."));
    }

    public async Task<Result<IReadOnlyList<AnexoResponse>>> ListarAsync(Guid casoId, CancellationToken cancellationToken = default)
    {
        var autorizado = await acesso.AutorizarAsync(casoId, escrita: false, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AnexoResponse>>(autorizado.Error);
        }

        var lista = await documentos.ListarAsync(CasoId.From(casoId), cancellationToken);

        return Result.Success<IReadOnlyList<AnexoResponse>>(
            lista.Where(d => Visivel(d, autorizado.Value)).OrderByDescending(d => d.CriadoEm).Select(ToResponse).ToList());
    }

    public async Task<Result<AnexoResponse>> ObterAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var documento = await CarregarAsync(casoId, documentoId, escrita: false, cancellationToken);

        return documento.IsFailure ? Result.Failure<AnexoResponse>(documento.Error) : Result.Success(ToResponse(documento.Value));
    }

    public async Task<Result<AnexoConteudo>> BaixarOriginalAsync(
        Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var documento = await CarregarAsync(casoId, documentoId, escrita: false, cancellationToken);
        if (documento.IsFailure)
        {
            return Result.Failure<AnexoConteudo>(documento.Error);
        }

        var d = documento.Value;

        return await LerArquivoAsync(d.ChaveOriginal, d.NomeArquivo, DetectorFormato.ContentType(d.Formato, d.NomeArquivo), cancellationToken);
    }

    public async Task<Result<AnexoConteudo>> ObterMarkdownAsync(
        Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var documento = await CarregarAsync(casoId, documentoId, escrita: false, cancellationToken);
        if (documento.IsFailure)
        {
            return Result.Failure<AnexoConteudo>(documento.Error);
        }

        if (documento.Value.ChaveMarkdown is not { } chave)
        {
            return Falha<AnexoConteudo>(ErrorCode.DRAFTING_NOT_READY);
        }

        return await LerArquivoAsync(
            chave, Path.GetFileNameWithoutExtension(documento.Value.NomeArquivo) + ".md", "text/plain; charset=utf-8", cancellationToken);
    }

    public async Task<Result<AnexoResponse>> ConfirmarRevisaoAsync(
        Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var autorizado = await acesso.AutorizarAsync(casoId, escrita: true, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<AnexoResponse>(autorizado.Error);
        }

        var documento = await CarregarAsync(autorizado.Value, documentoId, cancellationToken);
        if (documento.IsFailure)
        {
            return Result.Failure<AnexoResponse>(documento.Error);
        }

        // Não se confere o que ainda não foi lido.
        if (documento.Value.ChaveMarkdown is null)
        {
            return Falha<AnexoResponse>(ErrorCode.DRAFTING_NOT_READY);
        }

        documento.Value.ConfirmarRevisao(autorizado.Value.UsuarioId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(documento.Value));
    }

    public async Task<Result<AnexoResponse>> ReprocessarAsync(
        Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var documento = await CarregarAsync(casoId, documentoId, escrita: true, cancellationToken);
        if (documento.IsFailure)
        {
            return Result.Failure<AnexoResponse>(documento.Error);
        }

        if (documento.Value.EmProcessamento)
        {
            return Falha<AnexoResponse>(ErrorCode.DRAFTING_NOT_READY);
        }

        documento.Value.Reprocessar();
        fila.Enfileirar(documento.Value.TenantId, QueueItemKind.Conversao, documentoId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(documento.Value));
    }

    public async Task<Result> RemoverAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        var carregado = await CarregarAsync(casoId, documentoId, escrita: true, cancellationToken);
        if (carregado.IsFailure)
        {
            return Result.Failure(carregado.Error);
        }

        var documento = carregado.Value;

        // Ordem: índice → arquivos → banco. Falha no meio deixa o registro, e remover de novo completa.
        try
        {
            await vetores.RemoverDocumentoAsync(documento.TenantId, documento.CasoId.Value, documentoId, cancellationToken);
            await arquivos.RemoverAsync(documento.ChaveOriginal, cancellationToken);
            if (documento.ChaveMarkdown is { } markdown)
            {
                await arquivos.RemoverAsync(markdown, cancellationToken);
            }
        }
        catch (VectorStoreException)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_VECTOR_STORE_UNAVAILABLE));
        }
        catch (StorageException)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_STORAGE_UNAVAILABLE));
        }

        await fila.CancelarPorReferenciaAsync(documentoId, cancellationToken);
        documentos.Remover(documento);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<CasoDocumento>> CarregarAsync(Guid casoId, Guid documentoId, bool escrita, CancellationToken ct)
    {
        var autorizado = await acesso.AutorizarAsync(casoId, escrita, ct);

        return autorizado.IsFailure
            ? Result.Failure<CasoDocumento>(autorizado.Error)
            : await CarregarAsync(autorizado.Value, documentoId, ct);
    }

    private async Task<Result<CasoDocumento>> CarregarAsync(AcessoCaso autorizado, Guid documentoId, CancellationToken ct)
    {
        var documento = await documentos.ObterAsync(CasoDocumentoId.From(documentoId), ct);

        // Documento de OUTRO caso é "não encontrado", mesmo que quem pede veja os dois casos.
        return documento is null || documento.CasoId != autorizado.Caso.Id || !Visivel(documento, autorizado)
            ? Falha<CasoDocumento>(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND)
            : Result.Success(documento);
    }

    private async Task<Result<AnexoConteudo>> LerArquivoAsync(string chave, string nome, string contentType, CancellationToken ct)
    {
        try
        {
            var bytes = await arquivos.LerAsync(chave, ct);

            return bytes is null
                ? Falha<AnexoConteudo>(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND)
                : Result.Success(new AnexoConteudo(bytes, nome, contentType));
        }
        catch (StorageException)
        {
            return Falha<AnexoConteudo>(ErrorCode.DRAFTING_STORAGE_UNAVAILABLE);
        }
    }

    internal static bool Visivel(CasoDocumento documento, AcessoCaso acesso) =>
        !documento.Restrito || acesso.VeRestritos || documento.UsuarioIdResponsavel == acesso.UsuarioId;

    /// <summary>Sem diretório, sem controle e sem caracteres que quebrem cabeçalho HTTP ou a citação [arquivo, p. X].</summary>
    internal static string NomeSeguro(string nome)
    {
        var limpo = InputSanitizer.Clean(Path.GetFileName(nome.Replace('\\', '/')));
        var sb = new StringBuilder(limpo.Length);
        foreach (var c in limpo)
        {
            sb.Append(c is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '[' or ']' or ',' or ';' or '\n' or '\t' ? '_' : c);
        }

        var seguro = sb.ToString().Trim('.', ' ');
        if (seguro.Length <= 200)
        {
            return seguro;
        }

        var extensao = Path.GetExtension(seguro);

        return seguro[..(200 - extensao.Length)] + extensao;
    }

    private static async Task<byte[]?> LerAteAsync(Stream origem, long limite, CancellationToken ct)
    {
        using var destino = new MemoryStream();
        var buffer = new byte[81920];
        int lidos;
        while ((lidos = await origem.ReadAsync(buffer, ct)) > 0)
        {
            if (destino.Length + lidos > limite)
            {
                return null;
            }

            destino.Write(buffer, 0, lidos);
        }

        return destino.ToArray();
    }

    private static AnexoResponse ToResponse(CasoDocumento d) =>
        new(
            d.Id.Value, d.CasoId.Value, d.NomeArquivo, d.Formato.ToString().ToLowerInvariant(), d.TamanhoBytes, d.Status.ToString(),
            Mensagem(d), d.Origem.ToString().ToLowerInvariant(),
            d.Origem == OrigemTexto.Nativo ? null : MarkdownDocumento.RotuloConfianca(d.ConfiancaOcr),
            d.TipoDocumento, d.Area, d.DataDocumento, d.Paginas, d.Trechos, d.Restrito, d.RequerRevisaoHumana, d.RevisadoEm is not null,
            d.InjecaoSuspeita, Classificacao(d.ClassificacaoJson), d.CriadoEm, d.IndexadoEm);

    /// <summary>O motivo técnico fica no banco; aqui, só o que a pessoa pode fazer a respeito.</summary>
    private static string? Mensagem(CasoDocumento d) => d.Status switch
    {
        CasoDocumentoStatus.Falhou => "Não foi possível ler este arquivo. Confira se ele abre normalmente ou envie outra versão.",
        CasoDocumentoStatus.Indexado when d.PendenteDeRevisao => "Texto extraído com baixa confiança ou conteúdo suspeito: confira antes de usar.",
        CasoDocumentoStatus.Indexado => null,
        _ => "Em processamento.",
    };

    private static ClassificacaoDocumento? Classificacao(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClassificacaoDocumento>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Result<T> Falha<T>(ErrorCode codigo) => Result.Failure<T>(ErrorFactory.From(codigo));
}
