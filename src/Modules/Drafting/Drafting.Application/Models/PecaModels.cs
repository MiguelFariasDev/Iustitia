namespace Advocacia.Modules.Drafting.Application.Models;

/// <summary>
/// Corpo de <c>POST /pecas/gerar</c>. O usuário e o escritório vêm do JWT, nunca daqui.
/// A geração é SEMPRE assíncrona: a resposta é 202 com o id; acompanhe por status, stream ou polling.
/// </summary>
/// <param name="Pedidos">Opcional, mas recomendado: sem pedidos o modelo é instruído a não criá-los.</param>
/// <param name="DocumentosIds">Restringe a busca a estes anexos do caso. Vazio = todos os anexos do caso.</param>
/// <param name="CasoId">Obrigatório: é o limite do que a IA pode enxergar de documentos.</param>
/// <param name="DataFatos">Data relevante dos fatos, para conferir a vigência das normas citadas.</param>
/// <param name="Stream">Dica de cliente: se verdadeiro, a resposta traz <c>streamUrl</c> em destaque. Não muda o processamento.</param>
public sealed record GerarPecaRequest(
    string TipoPeca,
    string Area,
    string Fatos,
    string? Pedidos = null,
    IReadOnlyList<Guid>? DocumentosIds = null,
    string? InstrucoesAdicionais = null,
    bool Stream = false,
    Guid? CasoId = null,
    DateOnly? DataFatos = null);

/// <param name="Confirmar">Regerar custa GPU: exige confirmação explícita.</param>
public sealed record RegenerarPecaRequest(bool Confirmar, string? InstrucoesAdicionais = null);

/// <summary>Entrada já validada e sanitizada: é isto, e só isto, que chega ao prompt e ao banco.</summary>
public sealed record PecaInput(
    string TipoPeca,
    string Area,
    string Fatos,
    string Pedidos,
    string? InstrucoesAdicionais,
    IReadOnlyList<Guid> DocumentosIds,
    Guid? PecaOrigemId,
    Guid CasoId = default,
    Guid ClienteId = default,
    DateOnly? DataFatos = null);

/// <summary>Resposta imediata de <c>POST /pecas/gerar</c> (202 Accepted).</summary>
public sealed record SubmissaoResponse(
    Guid Id, string Status, string Mensagem, DateTimeOffset CriadoEm, string StatusUrl, string StreamUrl);

public sealed record StatusPecaResponse(
    Guid Id,
    string Status,
    string? Etapa,
    int Progresso,
    string Mensagem,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidoEm,
    string? Erro,
    string? LinkDownload);

/// <summary>Fonte exibida ao usuário. Sem ponto do Qdrant, sem URL de infraestrutura.</summary>
/// <param name="Rotulo">Como a fonte aparece citada no texto: "Código Civil, art. 186".</param>
/// <param name="UrlOrigem">Link da fonte OFICIAL (o acervo é informativo e não a substitui).</param>
public sealed record FonteResponse(
    Guid Id,
    Guid? DocumentoOrigemId,
    string NomeArquivo,
    int? Pagina,
    string Trecho,
    double Score,
    bool Citada,
    string? UrlOrigem,
    string? Rotulo = null,
    string? Grupo = null,
    string? Urn = null,
    string? Situacao = null,
    string? VersaoTexto = null,
    string? Arquivo = null);

public sealed record CitacaoResponse(
    Guid Id,
    string Texto,
    string Status,
    string Detalhe,
    bool Bloqueante,
    string? FonteUrl,
    string? Urn,
    string Decisao,
    string? Justificativa,
    DateTimeOffset? DecididoEm);

/// <param name="Decisao">"confirmada", "corrigida" ou "rejeitada".</param>
/// <param name="Justificativa">Obrigatória: como a pessoa conferiu (ex.: "conferido no DOU de 11/01/2002").</param>
public sealed record DecidirCitacaoRequest(string Decisao, string Justificativa);

/// <param name="DeclaroRevisao">Declaração expressa de que a minuta foi lida e conferida por profissional habilitado.</param>
/// <param name="TextoFinal">Texto corrigido; nulo mantém o gerado.</param>
public sealed record ConcluirRevisaoRequest(bool DeclaroRevisao, string? TextoFinal = null);

public sealed record RevisaoResponse(
    Guid PecaId, bool Concluida, int Pendentes, DateTimeOffset? RevisadoEm, IReadOnlyList<CitacaoResponse> Citacoes);

public sealed record ExportacaoPeca(byte[] Conteudo, string NomeArquivo, string ContentType);

public sealed record PecaDetalheResponse(
    Guid Id,
    string Status,
    string TipoPeca,
    string Area,
    int Versao,
    Guid? PecaOrigemId,
    string? TextoGerado,
    IReadOnlyList<FonteResponse> Fontes,
    IReadOnlyList<string> Avisos,
    string Aviso,
    long? TempoGeracaoMs,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidoEm,
    bool ExportacaoBloqueada = false,
    Guid? CasoId = null,
    IReadOnlyList<CitacaoResponse>? Citacoes = null,
    DateTimeOffset? RevisadoEm = null,
    string? TextoRevisado = null);

public sealed record PecaResumoResponse(
    Guid Id, string TipoPeca, string Area, string Status, int Versao, DateTimeOffset CriadoEm, DateTimeOffset? ConcluidoEm,
    Guid? CasoId = null, DateTimeOffset? RevisadoEm = null);

/// <summary>Métricas do ESCRITÓRIO (as da GPU, compartilhada, ficam em gpu_instance_logs).</summary>
public sealed record PecaMetricasResponse(
    int Total,
    int Concluidas,
    int Falhas,
    int Canceladas,
    int EmAndamento,
    int NaFila,
    double? TempoMedioGeracaoMs,
    decimal CustoEstimadoUsd,
    string MoedaCusto,
    string ObservacaoCusto,
    string Gpu,
    long TempoGeracaoTotalMs = 0,
    double GpuHorasUltimos30Dias = 0,
    decimal GpuCustoEstimadoUltimos30DiasUsd = 0,
    int AguardandoRevisao = 0,
    int AnexosEmProcessamento = 0,
    int EventosSeguranca30Dias = 0);

/// <summary>Evento NDJSON. Cada subtipo vira UMA linha de JSON válido; "tipo" identifica o evento.</summary>
public abstract record GenerationEvent(string Tipo);

public sealed record StatusEvent(string Etapa, string Mensagem, int Progresso) : GenerationEvent("status");

public sealed record FragmentoEvent(string Conteudo) : GenerationEvent("fragmento");

public sealed record FimEvent(
    Guid SolicitacaoId,
    int Versao,
    IReadOnlyList<FonteResponse> Fontes,
    IReadOnlyList<string> Avisos,
    string Aviso,
    long TempoGeracaoMs) : GenerationEvent("fim");

public sealed record ErroEvent(string Mensagem, Guid? SolicitacaoId) : GenerationEvent("erro");
