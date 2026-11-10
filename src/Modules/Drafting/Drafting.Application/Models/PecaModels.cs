namespace Advocacia.Modules.Drafting.Application.Models;

/// <summary>Corpo de <c>POST /pecas/gerar</c>. O usuário e o escritório vêm do JWT, nunca daqui.</summary>
/// <param name="DocumentosIds">Restringe a busca a estes documentos já indexados (ids do módulo Documents).</param>
/// <param name="Stream">Verdadeiro (padrão): NDJSON linha a linha. Falso: um JSON completo.</param>
/// <param name="PecaOrigemId">Se informado, a nova minuta é a versão seguinte daquela solicitação.</param>
public sealed record GerarPecaRequest(
    string TipoPeca,
    string Area,
    string Fatos,
    string Pedidos,
    IReadOnlyList<Guid>? DocumentosIds = null,
    string? InstrucoesAdicionais = null,
    bool Stream = true,
    Guid? PecaOrigemId = null);

/// <summary>Entrada já validada e sanitizada: é isto, e só isto, que chega ao prompt e ao banco.</summary>
public sealed record PecaInput(
    string TipoPeca,
    string Area,
    string Fatos,
    string Pedidos,
    string? InstrucoesAdicionais,
    IReadOnlyList<Guid> DocumentosIds,
    Guid? PecaOrigemId);

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

/// <summary>Resposta de <c>stream=false</c>.</summary>
public sealed record GerarPecaResponse(
    Guid SolicitacaoId,
    int Versao,
    string TextoGerado,
    IReadOnlyList<FonteResponse> Fontes,
    IReadOnlyList<string> Avisos,
    string Aviso,
    long TempoGeracaoMs,
    string? TraceId);

/// <summary>Evento NDJSON. Cada subtipo vira UMA linha de JSON válido; "tipo" identifica o evento.</summary>
public abstract record GenerationEvent(string Tipo);

public sealed record FragmentoEvent(string Conteudo) : GenerationEvent("fragmento");

public sealed record FimEvent(
    Guid SolicitacaoId,
    int Versao,
    IReadOnlyList<FonteResponse> Fontes,
    IReadOnlyList<string> Avisos,
    string Aviso,
    long TempoGeracaoMs,
    string? TraceId) : GenerationEvent("fim");

public sealed record ErroEvent(string Mensagem, Guid? SolicitacaoId) : GenerationEvent("erro");
