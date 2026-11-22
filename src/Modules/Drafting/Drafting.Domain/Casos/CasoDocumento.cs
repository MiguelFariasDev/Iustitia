using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Casos;

public readonly record struct CasoDocumentoId(Guid Value)
{
    public static CasoDocumentoId New() => new(Guid.NewGuid());

    public static CasoDocumentoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public enum FormatoDocumento
{
    Pdf = 0,
    Docx = 1,
    Doc = 2,
    Texto = 3,
    Markdown = 4,
    Imagem = 5,
    Planilha = 6,
    Email = 7,
    Audio = 8,
}

/// <summary>Como o texto foi obtido. Determina o quanto se pode confiar nele.</summary>
public enum OrigemTexto
{
    /// <summary>Texto do próprio arquivo.</summary>
    Nativo = 0,

    /// <summary>Reconhecimento óptico: pode ter erro de leitura.</summary>
    Ocr = 1,

    /// <summary>Transcrição automática de áudio: NÃO é a prova original.</summary>
    Transcricao = 2,
}

public enum CasoDocumentoStatus
{
    /// <summary>Original guardado, aguardando conversão.</summary>
    Recebido = 0,
    Convertendo = 1,

    /// <summary>Markdown pronto, aguardando classificação e embeddings (GPU).</summary>
    Convertido = 2,
    Indexando = 3,
    Indexado = 4,
    Falhou = 5,
}

/// <summary>
/// Anexo de um caso. O arquivo original e o Markdown normalizado vivem CIFRADOS no object storage; aqui
/// ficam só metadados, hash e estado. Os trechos (para reindexar sem reconverter) ficam em
/// <see cref="CasoDocumentoChunk"/> e, com os vetores, na coleção <c>documentos_caso</c>.
/// </summary>
public sealed class CasoDocumento : AggregateRoot<CasoDocumentoId>, IAuditable, IHasTenant
{
    public const int NomeMaxLength = 300;
    public const int ErroMaxLength = 500;

    /// <summary>Abaixo disto a leitura óptica é "baixa" e exige conferência humana.</summary>
    public const double ConfiancaOcrBaixa = 0.80;

    public Guid TenantId { get; private set; }

    public CasoId CasoId { get; private set; }

    public Guid ClienteId { get; private set; }

    /// <summary>Quem anexou.</summary>
    public Guid UsuarioIdResponsavel { get; private set; }

    public string NomeArquivo { get; private set; } = string.Empty;

    public FormatoDocumento Formato { get; private set; }

    public long TamanhoBytes { get; private set; }

    /// <summary>SHA-256 do ARQUIVO: o mesmo arquivo não entra duas vezes no mesmo caso (em outro caso, entra).</summary>
    public string HashSha256 { get; private set; } = string.Empty;

    public string ChaveOriginal { get; private set; } = string.Empty;

    public string? ChaveMarkdown { get; private set; }

    /// <summary>Verdadeiro: só quem anexou e o responsável pelo caso veem e usam o documento.</summary>
    public bool Restrito { get; private set; }

    public CasoDocumentoStatus Status { get; private set; }

    public string? Erro { get; private set; }

    public OrigemTexto Origem { get; private set; }

    /// <summary>0–1; nulo quando o texto é nativo.</summary>
    public double? ConfiancaOcr { get; private set; }

    public string? TipoDocumento { get; private set; }

    public string? Area { get; private set; }

    public DateOnly? DataDocumento { get; private set; }

    /// <summary>Classificação (apoio à busca e à revisão; nunca verdade absoluta), em JSON.</summary>
    public string? ClassificacaoJson { get; private set; }

    public int Paginas { get; private set; }

    public int Trechos { get; private set; }

    public bool RequerRevisaoHumana { get; private set; }

    public Guid? RevisadoPor { get; private set; }

    public DateTimeOffset? RevisadoEm { get; private set; }

    /// <summary>O conteúdo tem texto que tenta dar ordens ao assistente; esses trechos não vão ao prompt.</summary>
    public bool InjecaoSuspeita { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? IndexadoEm { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool EmProcessamento =>
        Status is CasoDocumentoStatus.Recebido or CasoDocumentoStatus.Convertendo
            or CasoDocumentoStatus.Convertido or CasoDocumentoStatus.Indexando;

    /// <summary>Leitura duvidosa que ninguém conferiu ainda.</summary>
    public bool PendenteDeRevisao => RequerRevisaoHumana && RevisadoEm is null;

    private CasoDocumento()
    {
        // EF Core.
    }

    private CasoDocumento(CasoDocumentoId id)
        : base(id)
    {
    }

    public static CasoDocumento Criar(
        CasoDocumentoId id, Caso caso, Guid usuarioId, string nomeArquivo, FormatoDocumento formato, long tamanhoBytes,
        string hash, string chaveOriginal, bool restrito, DateTimeOffset agora) =>
        new(id)
        {
            TenantId = caso.TenantId,
            CasoId = caso.Id,
            ClienteId = caso.ClienteId,
            UsuarioIdResponsavel = usuarioId,
            NomeArquivo = Cortar(nomeArquivo, NomeMaxLength),
            Formato = formato,
            TamanhoBytes = tamanhoBytes,
            HashSha256 = hash,
            ChaveOriginal = chaveOriginal,
            Restrito = restrito,
            Status = CasoDocumentoStatus.Recebido,
            CriadoEm = agora,
        };

    public void MarcarConvertendo() => Status = CasoDocumentoStatus.Convertendo;

    public void MarcarConvertido(
        string chaveMarkdown, OrigemTexto origem, double? confianca, int paginas, int trechos, bool injecaoSuspeita,
        DateOnly? dataDocumento)
    {
        ChaveMarkdown = chaveMarkdown;
        Origem = origem;
        ConfiancaOcr = confianca;
        Paginas = paginas;
        Trechos = trechos;
        InjecaoSuspeita = injecaoSuspeita;
        DataDocumento = dataDocumento ?? DataDocumento;

        // Transcrição nunca vale como prova; leitura óptica fraca e tentativa de injeção precisam de olho humano.
        RequerRevisaoHumana = origem == OrigemTexto.Transcricao
            || injecaoSuspeita
            || (origem == OrigemTexto.Ocr && (confianca ?? 0) < ConfiancaOcrBaixa);
        RevisadoEm = null;
        RevisadoPor = null;
        Status = CasoDocumentoStatus.Convertido;
        Erro = null;
    }

    /// <summary>
    /// A confiança da CLASSIFICAÇÃO (rótulos) não mexe em <see cref="RequerRevisaoHumana"/>, que é sobre a
    /// confiança da LEITURA: um contrato em texto nativo não vira duvidoso porque o modelo hesitou no tipo.
    /// </summary>
    public void AplicarClassificacao(string json, string? tipoDocumento, string? area)
    {
        ClassificacaoJson = json;
        TipoDocumento = tipoDocumento is null ? null : Cortar(tipoDocumento, 60);
        Area = area is null ? null : Cortar(area, 60);
    }

    public void MarcarIndexando() => Status = CasoDocumentoStatus.Indexando;

    public void MarcarIndexado(int trechos, DateTimeOffset agora)
    {
        Status = CasoDocumentoStatus.Indexado;
        Trechos = trechos;
        IndexadoEm = agora;
        Erro = null;
    }

    public void MarcarFalha(string erro)
    {
        Status = CasoDocumentoStatus.Falhou;
        Erro = Cortar(erro, ErroMaxLength);
    }

    /// <summary>Falha transitória: volta ao estado de espera da etapa, guardando o motivo.</summary>
    public void Reagendar(CasoDocumentoStatus etapa, string erro)
    {
        Status = etapa;
        Erro = Cortar(erro, ErroMaxLength);
    }

    public void Reprocessar()
    {
        Status = CasoDocumentoStatus.Recebido;
        Erro = null;
    }

    public void ConfirmarRevisao(Guid usuarioId, DateTimeOffset agora)
    {
        RevisadoPor = usuarioId;
        RevisadoEm = agora;
    }

    private static string Cortar(string valor, int max) => valor.Length <= max ? valor : valor[..max];
}

/// <summary>
/// Trecho de um anexo. Guardado para reindexar sem repetir OCR e para auditoria. Dado sigiloso: RLS por
/// escritório e texto cifrado em repouso.
/// </summary>
public sealed class CasoDocumentoChunk : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public CasoId CasoId { get; private set; }

    public CasoDocumentoId DocumentoId { get; private set; }

    public int Ordem { get; private set; }

    public int? Pagina { get; private set; }

    public string? Secao { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public string HashSha256 { get; private set; } = string.Empty;

    public int Tokens { get; private set; }

    public OrigemTexto Origem { get; private set; }

    public double? Confianca { get; private set; }

    /// <summary>Trecho com tentativa de dar ordens ao assistente: é indexado, mas nunca entra no prompt.</summary>
    public bool Suspeito { get; private set; }

    private CasoDocumentoChunk()
    {
        // EF Core.
    }

    public CasoDocumentoChunk(
        CasoDocumento documento, int ordem, int? pagina, string? secao, string texto, string hash, int tokens,
        OrigemTexto origem, double? confianca, bool suspeito)
        : base(IdPara(documento.Id.Value, ordem))
    {
        TenantId = documento.TenantId;
        CasoId = documento.CasoId;
        DocumentoId = documento.Id;
        Ordem = ordem;
        Pagina = pagina;
        Secao = secao is { Length: > 300 } ? secao[..300] : secao;
        Texto = texto;
        HashSha256 = hash;
        Tokens = tokens;
        Origem = origem;
        Confianca = confianca;
        Suspeito = suspeito;
    }

    /// <summary>Id determinístico: reprocessar o documento regrava os mesmos pontos do Qdrant.</summary>
    public static Guid IdPara(Guid documentoId, int ordem)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{documentoId:N}:{ordem}"));

        return new Guid(bytes.AsSpan(0, 16));
    }
}
