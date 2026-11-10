namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Seção <c>Drafting</c> do appsettings.</summary>
public sealed class DraftingOptions
{
    public const string SectionName = "Drafting";

    /// <summary>
    /// Desligado por padrão: ligar significa mandar fatos do caso (dado sigiloso de cliente) para a
    /// instância de IA configurada. Só habilite depois do checklist da ADR-072.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>Seção <c>Rag</c>.</summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public const int MinResultados = 5;
    public const int MaxResultados = 10;

    public string Colecao { get; set; } = "juridico_chunks";

    /// <summary>Trechos no contexto. Fica entre 5 e 10 mesmo que a configuração diga outra coisa.</summary>
    public int LimiteResultados { get; set; } = 8;

    /// <summary>Teto do contexto recuperado, em tokens APROXIMADOS (ver <see cref="CharsPorToken"/>).</summary>
    public int MaxTokensContexto { get; set; } = 8000;

    /// <summary>
    /// Estimativa de caracteres por token em português. Aproximação deliberadamente conservadora:
    /// o tokenizador real do modelo não está disponível aqui, e errar para menos estouraria o contexto.
    /// </summary>
    public double CharsPorToken { get; set; } = 3.5;

    /// <summary>Corta trechos com similaridade menor que isto. Nulo = só o limite de quantidade.</summary>
    public double? ScoreMinimo { get; set; }

    public int LimiteEfetivo => Math.Clamp(LimiteResultados, MinResultados, MaxResultados);
}

/// <summary>Seção <c>Drafting:Fila</c>: o worker que consome a fila e liga/desliga a GPU.</summary>
public sealed class FilaOptions
{
    public const string SectionName = "Drafting:Fila";

    /// <summary>De quanto em quanto tempo o worker olha a fila quando não há trabalho.</summary>
    public int IntervaloSegundos { get; set; } = 15;

    /// <summary>Itens reservados de uma vez (processamento em lote enquanto a GPU está ligada).</summary>
    public int LoteMax { get; set; } = 5;

    /// <summary>Gerações simultâneas no Ollama (precisa de <c>OLLAMA_NUM_PARALLEL</c> igual ou maior no servidor).</summary>
    public int Paralelismo { get; set; } = 1;

    /// <summary>Tentativas por item antes de marcar como Falhou (só falhas transitórias são repetidas).</summary>
    public int MaxTentativas { get; set; } = 3;

    /// <summary>Validade da reserva. Precisa ser maior que o timeout de uma geração; reserva vencida = worker caiu.</summary>
    public int ReservaMinutos { get; set; } = 15;

    /// <summary>Espera antes da 1ª retentativa; dobra a cada falha (30s, 60s, 120s…).</summary>
    public int BackoffBaseSegundos { get; set; } = 30;

    /// <summary>Um item que não conseguiu ser processado em tanto tempo falha (ex.: GPU indisponível por horas).</summary>
    public int ExpiraAposMinutos { get; set; } = 60;

    /// <summary>Intervalo entre os envios de texto parcial ao banco durante a geração (alimenta o stream e a checagem de cancelamento).</summary>
    public int ParcialIntervaloMs { get; set; } = 1000;

    /// <summary>Intervalo de consulta do endpoint de stream.</summary>
    public int StreamIntervaloMs { get; set; } = 700;

    /// <summary>O stream se encerra sozinho depois disto, mesmo sem a peça terminar.</summary>
    public int StreamMaxMinutos { get; set; } = 30;

    public TimeSpan Backoff(int tentativa) =>
        TimeSpan.FromSeconds(BackoffBaseSegundos * Math.Pow(2, Math.Max(0, tentativa - 1)));
}

/// <summary>Seção <c>Drafting:Custo</c>.</summary>
public sealed class CustoOptions
{
    public const string SectionName = "Drafting:Custo";

    /// <summary>Preço por hora da GPU alugada, em dólares. Valor de ESTIMATIVA: confira na fatura da Vast.ai.</summary>
    public decimal GpuUsdPorHora { get; set; } = 0.40m;
}

/// <summary>Seção <c>Drafting:Ingestao</c>.</summary>
public sealed class IngestaoOptions
{
    public const string SectionName = "Drafting:Ingestao";

    public int MaxMegabytes { get; set; } = 20;

    public int MaxPaginas { get; set; } = 500;
}

/// <summary>Seção <c>VastAi</c>. A chave NUNCA vai no appsettings versionado.</summary>
public sealed class VastAiOptions
{
    public const string SectionName = "VastAi";

    /// <summary>
    /// Falso = o Ollama é um serviço sempre ligado (desenvolvimento local, servidor próprio): o
    /// orquestrador não chama a Vast.ai, só espera o Ollama responder, e nunca desliga nada.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string ApiBaseUrl { get; set; } = "https://console.vast.ai/api/v0";

    public string ApiKey { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    /// <summary>De quanto em quanto tempo consulta o estado da instância enquanto espera ela subir.</summary>
    public int PollingIntervalSeconds { get; set; } = 10;

    /// <summary>Prazo para a instância ficar "running" E o Ollama responder.</summary>
    public int StartTimeoutMinutes { get; set; } = 10;

    /// <summary>Carência sem trabalho antes de parar a instância.</summary>
    public int IdleTimeoutMinutes { get; set; } = 10;

    /// <summary>Tentativas por chamada à API da Vast.ai (rede/5xx), com espera crescente.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Espera base entre tentativas da API (dobra a cada falha: 1s, 2s, 4s…).</summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>Falhas seguidas que abrem o circuito (para de chamar a Vast.ai por um tempo).</summary>
    public int CircuitBreakerFalhas { get; set; } = 3;

    public int CircuitBreakerPausaMinutos { get; set; } = 5;

    /// <summary>
    /// Descobre o endereço do Ollama pelo IP/porta públicos que a Vast.ai informa a cada subida.
    /// Desligado: usa <c>Ollama:BaseUrl</c> fixo (recomendado, atrás de TLS/túnel). Ligado, o
    /// endereço é http:// sobre IP público — só com <c>Ollama:AllowInsecureHttp</c> (ver ADR-072).
    /// </summary>
    public bool ResolverEndpointPelaVast { get; set; }

    /// <summary>Porta interna do Ollama no contêiner (a externa é sorteada pela Vast.ai).</summary>
    public int PortaOllama { get; set; } = 11434;
}
