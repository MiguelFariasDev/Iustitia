namespace Advocacia.Modules.Drafting.Application.Models;

/// <summary>
/// Corpo de <c>POST /pecas/gerar</c>. O usuário e o escritório vêm do JWT, nunca daqui.
/// A geração é SEMPRE assíncrona: a resposta é 202 com o id; acompanhe por status, stream ou polling.
/// </summary>
/// <param name="Pedidos">Opcional, mas recomendado: sem pedidos o modelo é instruído a não criá-los.</param>
/// <param name="DocumentosIds">Restringe a busca a estes documentos já indexados (ids do acervo).</param>
/// <param name="Stream">Dica de cliente: se verdadeiro, a resposta traz <c>streamUrl</c> em destaque. Não muda o processamento.</param>
public sealed record GerarPecaRequest(
    string TipoPeca,
    string Area,
    string Fatos,
    string? Pedidos = null,
    IReadOnlyList<Guid>? DocumentosIds = null,
    string? InstrucoesAdicionais = null,
    bool Stream = false);

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
    Guid? PecaOrigemId);

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
public sealed record FonteResponse(
    Guid Id,
    Guid? DocumentoOrigemId,
    string NomeArquivo,
    int? Pagina,
    string Trecho,
    double Score,
    bool Citada,
    string? UrlOrigem);

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
    bool ExportacaoBloqueada = false);

public sealed record PecaResumoResponse(
    Guid Id, string TipoPeca, string Area, string Status, int Versao, DateTimeOffset CriadoEm, DateTimeOffset? ConcluidoEm);

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
    string Gpu);

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
