namespace Advocacia.Modules.Integrations.CNJ.Application.Models;

/// <summary>
/// Critério de consulta ao DJEN/CNJ. Paginação é 1-based (como a API do CNJ) e
/// <see cref="PageSize"/> é limitado pelo cliente ao máximo aceito pela API.
///
/// Dois modos de consulta, mutuamente complementares (use as fábricas
/// <see cref="ByPeriod"/> e <see cref="ByProcess"/> em vez do construtor):
///
///   - <b>Por período</b> (<see cref="StartDate"/>/<see cref="EndDate"/> + opcionalmente
///     <see cref="Tribunal"/>): varredura do diário. É o modo da captura diária.
///   - <b>Por processo</b> (<see cref="CnjNumber"/>): devolve o histórico de comunicações
///     daquele processo, sem precisar de intervalo de datas. É o modo que interessa quando
///     o escritório já sabe quais processos acompanha — e o que a Fase 2 usará ao vincular
///     publicações a processos cadastrados.
///
/// Uma consulta sem período E sem processo puxaria o diário inteiro do país; o cliente
/// rejeita esse caso (ver DjenClient).
/// </summary>
/// <param name="StartDate">Início do intervalo de disponibilização (inclusivo).</param>
/// <param name="EndDate">Fim do intervalo de disponibilização (inclusivo).</param>
/// <param name="Tribunal">Sigla do tribunal (ex.: "TJSP"). Nulo consulta todos.</param>
/// <param name="CnjNumber">Número do processo (com ou sem máscara) para consulta dirigida.</param>
/// <param name="Page">Número da página, começando em 1.</param>
/// <param name="PageSize">Itens por página.</param>
public sealed record CnjPublicationQuery(
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Tribunal,
    string? CnjNumber = null,
    int Page = 1,
    int PageSize = 100)
{
    /// <summary>Varredura do diário por intervalo de datas, opcionalmente restrita a um tribunal.</summary>
    public static CnjPublicationQuery ByPeriod(
        DateOnly startDate, DateOnly endDate, string? tribunal = null, int page = 1, int pageSize = 100) =>
        new(startDate, endDate, tribunal, CnjNumber: null, page, pageSize);

    /// <summary>
    /// Consulta dirigida ao histórico de um processo. O intervalo de datas é opcional: sem
    /// ele, o DJEN devolve todas as comunicações já publicadas para o processo.
    /// </summary>
    public static CnjPublicationQuery ByProcess(
        string cnjNumber,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        int page = 1,
        int pageSize = 100) =>
        new(startDate, endDate, Tribunal: null, cnjNumber, page, pageSize);

    /// <summary>Se a consulta tem algum critério de recorte — sem nenhum, puxaria o diário nacional inteiro.</summary>
    public bool HasCriteria => StartDate is not null || EndDate is not null || !string.IsNullOrWhiteSpace(CnjNumber);

    /// <summary>Rótulo curto e de BAIXA cardinalidade para logs e spans (nunca para tags de métrica).</summary>
    public string Describe() => string.IsNullOrWhiteSpace(CnjNumber)
        ? $"tribunal={Tribunal ?? "todos"} periodo={StartDate:yyyy-MM-dd}..{EndDate:yyyy-MM-dd}"
        : $"processo={CnjNumber}";
}
