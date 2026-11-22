using Advocacia.Modules.Drafting.Application.Busca;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Configuração da ingestão do acervo normativo (seção <c>Ingestao</c>). O binder do .NET ANEXA itens a um array
/// que já tem valores: se os padrões estivessem na propriedade, a configuração só conseguiria AUMENTAR o escopo,
/// nunca reduzi-lo. Por isso as listas nascem vazias e os padrões entram em <see cref="Incluidas"/> e <see cref="Excluidas"/>.
/// </summary>
public sealed class NormasIngestaoOptions
{
    public const string SectionName = "Ingestao";

    /// <summary>
    /// "Local": lê os Markdowns já baixados em <see cref="DiretorioLocal"/>, sem rede e sem tocar na pasta.
    /// "Git": clona/atualiza (sparse, raso) em <see cref="DiretorioLocal"/> antes de ler.
    /// </summary>
    public string Modo { get; set; } = "Local";

    /// <summary>
    /// Começa pelo que mais se usa (Constituição, códigos, leis complementares, súmulas e temas dos tribunais
    /// superiores). Desligue para expandir a todo o escopo de <see cref="PastasIncluidas"/>.
    /// </summary>
    public bool SomenteAltaUtilizacao { get; set; } = true;

    public string RepositorioUrl { get; set; } = "https://github.com/ctresb/leis.git";

    public string Branch { get; set; } = "main";

    /// <summary>Caminhos do repositório, na caixa exata do Git. Vazio = Constituição, leis federais e jurisprudência.</summary>
    public string[] PastasIncluidas { get; set; } = [];

    /// <summary>Nomes excluídos em QUALQUER nível, além dos padrões. Todo segmento começando com "_" ou "." também é excluído.</summary>
    public string[] PastasExcluidas { get; set; } = [];

    public IReadOnlyList<string> Incluidas =>
        PastasIncluidas.Length == 0 ? ["constituicao.md", "federal", "jurisprudencia"] : PastasIncluidas.Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>As exclusões padrão não são negociáveis por configuração: só se acrescenta a elas.</summary>
    public IReadOnlyList<string> Excluidas =>
        PastasExcluidas.Concat(["_fonte", ".git", "scripts", "estaduais", "municipais", "reguladores", "tratados"])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

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

    /// <summary>
    /// Piso para REUNIR pedaços. Artigo curto fica sozinho mesmo abaixo disto: nunca se juntam dois artigos,
    /// porque a citação precisa apontar para um só.
    /// </summary>
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

    /// <summary>Por coleção e por vetor de consulta.</summary>
    public int CandidatosVetor { get; set; } = 30;

    /// <summary>Por coleção.</summary>
    public int CandidatosBm25 { get; set; } = 30;

    /// <summary>Candidatos que chegam ao reranker, somando as coleções (faixa pedida: 30 a 100).</summary>
    public int CandidatosRerank { get; set; } = 100;

    public MetodoFusao Fusao { get; set; } = MetodoFusao.Rrf;

    /// <summary>Constante k da fusão por ranking (RRF). 60 é o valor da literatura.</summary>
    public int RrfK { get; set; } = 60;

    /// <summary>Peso da lista de referência exata ("art. 186 do CC") na fusão.</summary>
    public double PesoReferencia { get; set; } = 2.0;

    /// <summary>Trechos por norma entre os reranqueados: uma lei grande não pode monopolizar o contexto.</summary>
    public int MaxPorNorma { get; set; } = 3;

    public int ResultadosMin { get; set; } = 5;

    public int ResultadosMax { get; set; } = 10;

    /// <summary>Vagas reservadas aos anexos do caso, quando houver (eles têm prioridade).</summary>
    public int VagasCaso { get; set; } = 4;

    public int VagasConstituicao { get; set; } = 2;

    public int VagasLeis { get; set; } = 4;

    public int VagasJurisprudencia { get; set; } = 3;

    /// <summary>
    /// Fração do melhor score jurídico que um trecho precisa ter para ocupar vaga RESERVADA. Sem isto, a vaga da
    /// Constituição era preenchida com qualquer artigo, tirando lugar de lei e jurisprudência pertinentes.
    /// </summary>
    public double RelevanciaMinimaReserva { get; set; } = 0.80;

    public bool IncluirRevogadas { get; set; } = true;
}
