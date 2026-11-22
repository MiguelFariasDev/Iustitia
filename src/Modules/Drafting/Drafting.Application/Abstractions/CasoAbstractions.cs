using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Seguranca;

namespace Advocacia.Modules.Drafting.Application.Abstractions;

// ---------------------------------------------------------------- persistência

public interface ICasoRepository
{
    void Add(Caso caso);

    /// <summary>Marca o membro NOVO como Added (o EF trataria um filho com chave preenchida como existente).</summary>
    void AddMembro(CasoMembro membro);

    Task<Caso?> ObterAsync(CasoId id, CancellationToken cancellationToken = default);

    /// <param name="usuarioId">Nulo = todos do escritório (gestão). Com valor: responsável ou membro.</param>
    Task<IReadOnlyList<Caso>> ListarAsync(
        Guid? usuarioId, Guid? clienteId, int skip, int take, CancellationToken cancellationToken = default);
}

public interface ICasoDocumentoRepository
{
    void Add(CasoDocumento documento);

    void Remover(CasoDocumento documento);

    Task<CasoDocumento?> ObterAsync(CasoDocumentoId id, CancellationToken cancellationToken = default);

    Task<CasoDocumento?> ObterPorHashAsync(CasoId casoId, string hash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CasoDocumento>> ListarAsync(CasoId casoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CasoDocumentoChunk>> ListarChunksAsync(CasoDocumentoId id, CancellationToken cancellationToken = default);

    /// <summary>Troca os trechos do documento (a gravação acontece no SaveChanges do chamador).</summary>
    Task SubstituirChunksAsync(
        CasoDocumentoId id, IReadOnlyList<CasoDocumentoChunk> chunks, CancellationToken cancellationToken = default);
}

public interface IEventoSegurancaRepository
{
    void Add(EventoSegurancaIa evento);
}

/// <summary>Arquivos de um caso no object storage, sempre CIFRADOS (AES-256-GCM) antes de sair do processo.</summary>
public interface ICasoArquivoStore
{
    Task GravarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default);

    Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default);

    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);
}

/// <summary>Falha do object storage. Transitória: a conversão é repetida.</summary>
public sealed class StorageException(string message, Exception? inner = null) : Exception(message, inner);

// ---------------------------------------------------------------- casos e anexos

public sealed record AcessoCaso(Caso Caso, Guid UsuarioId, bool PodeEscrever, bool VeRestritos);

/// <summary>Única porta de autorização de caso. "Não pode ver" responde "não encontrado", para não revelar que o caso existe.</summary>
public interface ICasoAcessoService
{
    Task<Result<AcessoCaso>> AutorizarAsync(Guid casoId, bool escrita, CancellationToken cancellationToken = default);
}

public interface ICasoService
{
    Task<Result<CasoResponse>> CriarAsync(CriarCasoRequest request, CancellationToken cancellationToken = default);

    Task<Result<CasoResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CasoResponse>>> ListarAsync(
        Guid? clienteId, int pagina, int tamanho, CancellationToken cancellationToken = default);

    Task<Result<CasoResponse>> AtualizarAsync(Guid id, AtualizarCasoRequest request, CancellationToken cancellationToken = default);

    Task<Result<CasoResponse>> ConcederAcessoAsync(Guid id, ConcederAcessoRequest request, CancellationToken cancellationToken = default);

    Task<Result<CasoResponse>> RevogarAcessoAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IAnexoService
{
    Task<Result<UploadAnexoResponse>> EnviarAsync(
        Guid casoId, Stream conteudo, string nomeArquivo, long tamanhoDeclarado, bool restrito,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AnexoResponse>>> ListarAsync(Guid casoId, CancellationToken cancellationToken = default);

    Task<Result<AnexoResponse>> ObterAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    Task<Result<AnexoConteudo>> BaixarOriginalAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    Task<Result<AnexoConteudo>> ObterMarkdownAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    /// <summary>Uma pessoa conferiu o texto extraído contra o original.</summary>
    Task<Result<AnexoResponse>> ConfirmarRevisaoAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    Task<Result<AnexoResponse>> ReprocessarAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    /// <summary>Apaga do índice vetorial, dos trechos, do storage (original e Markdown) e do banco.</summary>
    Task<Result> RemoverAsync(Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);
}

/// <summary>Registro de tentativas suspeitas: tipo, origem e hash — nunca o conteúdo.</summary>
public interface ISegurancaIaService
{
    Task RegistrarAsync(
        Guid tenantId, Guid? usuarioId, string tipo, string origem, string? conteudo, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- conversão

public sealed record DocumentoParaConverter(byte[] Conteudo, string NomeArquivo, FormatoDocumento Formato);

/// <param name="Markdown">Texto já em Markdown, sem o cabeçalho do documento.</param>
/// <param name="Confianca">0–1 quando o texto veio de OCR ou transcrição; nulo para texto nativo.</param>
public sealed record BlocoConvertido(int? Pagina, string? Secao, string Markdown, OrigemTexto Origem, double? Confianca = null);

public sealed record DocumentoConvertido(
    string? Titulo,
    IReadOnlyList<BlocoConvertido> Blocos,
    IReadOnlyDictionary<string, string> Metadados,
    IReadOnlyList<string> Avisos)
{
    /// <summary>A origem menos confiável entre os blocos: um documento com uma página de OCR é tratado como OCR.</summary>
    public OrigemTexto Origem => Blocos.Count == 0 ? OrigemTexto.Nativo : Blocos.Max(b => b.Origem);

    /// <summary>Média ponderada pelo tamanho, só dos blocos não nativos.</summary>
    public double? Confianca
    {
        get
        {
            var medidos = Blocos.Where(b => b.Confianca is not null).ToList();
            var peso = medidos.Sum(b => (double)Math.Max(1, b.Markdown.Length));

            return medidos.Count == 0 ? null : medidos.Sum(b => b.Confianca!.Value * Math.Max(1, b.Markdown.Length)) / peso;
        }
    }

    public int Paginas => Blocos.Where(b => b.Pagina is not null).Select(b => b.Pagina!.Value).DefaultIfEmpty(0).Max();
}

/// <summary>Converte qualquer formato suportado em Markdown normalizado. Roda no worker, só com CPU.</summary>
public interface IConversorDocumentoService
{
    Task<Result<DocumentoConvertido>> ConverterAsync(DocumentoParaConverter documento, CancellationToken cancellationToken = default);
}

/// <summary>Um formato. O serviço escolhe pela propriedade <see cref="Formatos"/>.</summary>
public interface IConversorFormato
{
    IReadOnlyList<FormatoDocumento> Formatos { get; }

    Task<Result<DocumentoConvertido>> ConverterAsync(DocumentoParaConverter documento, CancellationToken cancellationToken = default);
}

/// <param name="Confianca">0–1, média ponderada por palavra.</param>
public sealed record ResultadoOcr(string Texto, double Confianca);

public interface IOcrService
{
    Task<bool> DisponivelAsync(CancellationToken cancellationToken = default);

    Task<Result<ResultadoOcr>> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken = default);
}

public sealed record SegmentoTranscricao(TimeSpan Inicio, TimeSpan Fim, string Texto, double? Confianca);

public sealed record ResultadoTranscricao(IReadOnlyList<SegmentoTranscricao> Segmentos, double? Confianca);

public interface ITranscricaoService
{
    bool Disponivel { get; }

    Task<Result<ResultadoTranscricao>> TranscreverAsync(byte[] audio, string nomeArquivo, CancellationToken cancellationToken = default);
}

public sealed record MetadadosExtraidos(
    DateOnly? DataDocumento,
    IReadOnlyList<string> Datas,
    IReadOnlyList<string> Valores,
    IReadOnlyList<string> NumerosProcesso,
    IReadOnlyList<string> Partes);

/// <summary>Extração determinística (regras), sem IA: base de conferência do que a IA disser.</summary>
public interface IExtracaoMetadadosService
{
    MetadadosExtraidos Extrair(string texto);
}

public interface IClassificacaoDocumentoService
{
    /// <summary>Classifica com o modelo (JSON Schema) e confere contra o texto; sem modelo, cai na heurística.</summary>
    Task<ClassificacaoDocumento> ClassificarAsync(
        string nomeArquivo, string texto, OrigemTexto origem, double? confianca, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- IA estruturada

/// <summary>Resposta do modelo restrita a um JSON Schema (parâmetro <c>format</c> do Ollama), sem streaming.</summary>
public interface ILlmEstruturado
{
    Task<string> CompletarJsonAsync(
        string system, string user, string jsonSchema, int maxTokens, CancellationToken cancellationToken = default);
}

public interface IConsultaEnriquecidaService
{
    Task<ConsultaEstruturada> ExtrairAsync(
        PecaInput input, IReadOnlyList<CasoDocumento> documentos, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- busca

/// <summary>Filtro da coleção de anexos. Não existe busca sem tenant E caso: ambos são obrigatórios no construtor.</summary>
public sealed record FiltroCaso
{
    public FiltroCaso(
        Guid tenantId, Guid casoId, Guid usuarioId, bool veRestritos, IReadOnlyList<Guid>? documentosIds = null)
    {
        if (tenantId == Guid.Empty || casoId == Guid.Empty || usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Busca em documentos do caso exige tenant, caso e usuário.");
        }

        TenantId = tenantId;
        CasoId = casoId;
        UsuarioId = usuarioId;
        VeRestritos = veRestritos;
        DocumentosIds = documentosIds ?? [];
    }

    public Guid TenantId { get; }

    public Guid CasoId { get; }

    public Guid UsuarioId { get; }

    /// <summary>Responsável pelo caso vê os restritos; os demais só os próprios.</summary>
    public bool VeRestritos { get; }

    public IReadOnlyList<Guid> DocumentosIds { get; }
}

public sealed record CasoVectorPoint(
    Guid Id, float[] Denso, VetorEsparso Esparso, string Texto, ChunkMetadata Meta, DateTimeOffset DataUpload);

/// <summary>Coleção <c>documentos_caso</c>. Toda leitura e toda exclusão passam pelo filtro de tenant e caso.</summary>
public interface ICasoVectorStore
{
    Task UpsertAsync(IReadOnlyList<CasoVectorPoint> pontos, CancellationToken cancellationToken = default);

    Task RemoverDocumentoAsync(Guid tenantId, Guid casoId, Guid documentoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> BuscarDensoAsync(
        FiltroCaso filtro, float[] vetor, int limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> BuscarBm25Async(
        FiltroCaso filtro, VetorEsparso consulta, int limite, CancellationToken cancellationToken = default);
}

public sealed record ColecaoInfo(string Nome, bool Existe, ulong Pontos);

/// <summary>Administração das coleções: cria as cinco (int8, vetores em disco, BM25, índices de payload) e informa o estado.</summary>
public interface IQdrantService
{
    Task GarantirColecoesAsync(CancellationToken cancellationToken = default);

    Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColecaoInfo>> EstadoAsync(CancellationToken cancellationToken = default);
}

/// <summary>Vetores e textos de uma consulta, prontos para as buscas (embutidos UMA vez, usados em todas).</summary>
public sealed record ConsultaPreparada(
    ConsultaEstruturada Estruturada,
    IReadOnlyList<float[]> Vetores,
    string TextoBm25,
    IReadOnlyList<ReferenciaLegal> Referencias,
    string? Uf,
    DateOnly? DataFatos);

public interface IBuscaDocumentosCasoService
{
    Task<IReadOnlyList<VectorHit>> BuscarAsync(
        FiltroCaso filtro, ConsultaPreparada consulta, CancellationToken cancellationToken = default);
}

public interface IBuscaJuridicaService
{
    /// <summary>Candidatos das três coleções públicas, fundidos, deduplicados e reranqueados.</summary>
    Task<IReadOnlyList<VectorHit>> BuscarAsync(ConsultaPreparada consulta, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- revisão humana

public interface IRevisaoPecaService
{
    Task<Result<RevisaoResponse>> ObterAsync(Guid pecaId, CancellationToken cancellationToken = default);

    Task<Result<CitacaoResponse>> DecidirAsync(
        Guid pecaId, Guid citacaoId, DecidirCitacaoRequest request, CancellationToken cancellationToken = default);

    Task<Result<RevisaoResponse>> ConcluirAsync(
        Guid pecaId, ConcluirRevisaoRequest request, CancellationToken cancellationToken = default);

    /// <param name="formato">"docx" ou "md".</param>
    Task<Result<ExportacaoPeca>> ExportarAsync(Guid pecaId, string formato, CancellationToken cancellationToken = default);
}

/// <summary>Monta o DOCX da peça revisada (OpenXML na infraestrutura).</summary>
public interface IExportadorDocx
{
    byte[] Gerar(string titulo, string texto, string rodape);
}
