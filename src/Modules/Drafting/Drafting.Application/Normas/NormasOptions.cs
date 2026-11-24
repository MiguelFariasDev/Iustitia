namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Configuração da ingestão do acervo normativo (seção <c>Ingestao</c>). Arrays em vez de listas: o binder
/// SUBSTITUI um array, mas ANEXARIA itens a uma lista com valores padrão (duplicando as pastas).
/// </summary>
public sealed class NormasIngestaoOptions
{
    public const string SectionName = "Ingestao";

    public string RepositorioUrl { get; set; } = "https://github.com/ctresb/leis.git";

    public string Branch { get; set; } = "main";

    /// <summary>
    /// Caminhos do repositório (relativos à raiz, em MINÚSCULAS/MAIÚSCULAS exatamente como no Git: o
    /// sistema de arquivos do Linux e o sparse-checkout diferenciam caixa). Ex.: "municipais/CE/fortaleza".
    /// </summary>
    public string[] PastasIncluidas { get; set; } =
    [
        "constituicao.md", "federal", "jurisprudencia", "tratados", "reguladores", "estaduais/CE", "municipais/CE/fortaleza",
    ];

    /// <summary>Nomes de pasta/arquivo excluídos em QUALQUER nível. Todo segmento começando com "_" também é excluído.</summary>
    public string[] PastasExcluidas { get; set; } = ["_fonte", ".git", "scripts"];

    public int ProfundidadeGit { get; set; } = 1;

    /// <summary>Baixar os originais (_fonte/*.tar.zst). Falso por padrão; ligado só para auditoria pontual.</summary>
    public bool BaixarOriginais { get; set; }

    /// <summary>Onde o clone parcial fica no disco do worker.</summary>
    public string DiretorioLocal { get; set; } = "/var/lib/advocacia/leis";

    /// <summary>Identifica a origem nos registros (campo Origem).</summary>
    public string Origem { get; set; } = "ctresb/leis";

    public bool WorkerHabilitado { get; set; }

    public int IntervaloHoras { get; set; } = 24;

    /// <summary>Teto de arquivos processados por ciclo: a primeira carga avança em fatias, por prioridade.</summary>
    public int LimiteArquivosPorCiclo { get; set; } = 5000;

    public int LoteEmbedding { get; set; } = 32;

    public int ParalelismoArquivos { get; set; } = 2;

    public int TamanhoMaximoArquivoMb { get; set; } = 20;

    public NormasFiltroOptions Filtros { get; set; } = new();

    public StorageOptions Storage { get; set; } = new();
}

/// <summary>Recorte do acervo por UF, município, tipo, área e período. Vazio = sem restrição.</summary>
public sealed class NormasFiltroOptions
{
    public string[] Ufs { get; set; } = [];

    public string[] Municipios { get; set; } = [];

    public string[] Tipos { get; set; } = [];

    public string[] Areas { get; set; } = [];

    public int? AnoDe { get; set; }

    public int? AnoAte { get; set; }
}

public sealed class StorageOptions
{
    /// <summary>"Local" (disco; desenvolvimento) ou "S3" (Cloudflare R2, Backblaze B2 ou qualquer S3-compatível).</summary>
    public string Provedor { get; set; } = "Local";

    public string Bucket { get; set; } = "juridico";

    /// <summary>Prefixo das chaves. Estrutura: {Prefixo}/markdown/..., {Prefixo}/originais/..., {Prefixo}/auditoria/...</summary>
    public string Prefixo { get; set; } = "juridico";

    /// <summary>Endpoint S3 (https). R2: https://&lt;conta&gt;.r2.cloudflarestorage.com · B2: https://s3.&lt;regiao&gt;.backblazeb2.com</summary>
    public string? Endpoint { get; set; }

    public string Regiao { get; set; } = "auto";

    /// <summary>Segredos: SÓ por variável de ambiente (Ingestao__Storage__AccessKey / SecretKey).</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string DiretorioLocal { get; set; } = "/var/lib/advocacia/storage";
}

/// <summary>Parâmetros do chunker jurídico (seção <c>Rag:Chunking</c>).</summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Rag:Chunking";

    public int MinTokens { get; set; } = 400;

    public int AlvoTokens { get; set; } = 700;

    public int MaxTokens { get; set; } = 900;

    /// <summary>Português jurídico rende ~3,3 caracteres por token no tokenizer do BGE-M3; estimativa conservadora.</summary>
    public double CharsPorToken { get; set; } = 3.3;

    public bool IncluirEmentaEmTodos { get; set; } = true;

    public int EmentaMaxChars { get; set; } = 200;
}

/// <summary>Parâmetros da busca híbrida (seção <c>Rag:Busca</c>).</summary>
public sealed class BuscaNormasOptions
{
    public const string SectionName = "Rag:Busca";

    public int CandidatosVetor { get; set; } = 60;

    public int CandidatosPalavraChave { get; set; } = 40;

    /// <summary>Constante k da fusão por ranking (RRF). 60 é o valor da literatura.</summary>
    public int RrfK { get; set; } = 60;

    public int ResultadosMin { get; set; } = 5;

    public int ResultadosMax { get; set; } = 10;

    public bool IncluirRevogadas { get; set; } = true;

    /// <summary>Embaralhar fontes de hierarquia equivalente é pior que ordenar por autoridade; ver NormaReranker.</summary>
    public double PesoSemantico { get; set; } = 1.0;
}
