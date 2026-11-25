namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

// Espelho do JSON do juscraper (camelCase). Só o que o Iustitia usa: campos extras (partes, valor
// da causa, juiz…) são ignorados pelo desserializador — partes trazem nomes de pessoas e não têm
// uso aqui (cliente e responsável nunca vêm da consulta).

/// <summary>GET /v1/processos/{numero}.</summary>
internal sealed record JuscraperResultado(
    string? NumeroProcesso,
    string? Tribunal,
    bool Encontrado,
    IReadOnlyList<JuscraperProcesso>? Processos,
    DateTimeOffset? ConsultadoEm,
    IReadOnlyList<string>? Avisos);

internal sealed record JuscraperProcesso(
    string? NumeroProcesso,
    string? Tribunal,
    string? Grau,
    string? Sistema,
    DateOnly? DataAjuizamento,
    bool EmSegredoDeJustica,
    string? Classe,
    string? OrgaoJulgador,
    IReadOnlyList<string>? Assuntos,
    IReadOnlyList<JuscraperMovimento>? Movimentos,
    int? CodigoMunicipioIbge,
    bool MovimentosCompletos);

/// <param name="Id">Estável entre consultas (SHA-256 de processo, instância, data, nome e complemento).</param>
internal sealed record JuscraperMovimento(
    string? Id,
    string? Nome,
    DateTimeOffset? DataHora,
    string? Complemento,
    bool PossuiDocumento,
    string? Instancia);

/// <summary>GET /v1/processos/{numero}/movimentacoes.</summary>
internal sealed record JuscraperMovimentacoes(
    string? NumeroProcesso,
    string? Tribunal,
    bool Encontrado,
    bool Completas,
    IReadOnlyList<JuscraperMovimento>? Movimentos,
    DateTimeOffset? ConsultadoEm,
    IReadOnlyList<string>? Avisos);

/// <summary>ProblemDetails (RFC 9457) com as extensões do juscraper.</summary>
internal sealed record JuscraperProblema(
    string? Title,
    int? Status,
    string? Codigo,
    string? Tribunal,
    bool? Transitorio);
