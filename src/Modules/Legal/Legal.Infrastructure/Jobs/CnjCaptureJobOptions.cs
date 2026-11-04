namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Seção "Jobs:CnjCapture" do appsettings. O agendamento (Enabled/CronExpression) é lido por
/// <c>LegalRecurringJobsRegistrar</c>; o restante é lido pelo próprio job.
/// </summary>
public sealed class CnjCaptureJobOptions
{
    public const string SectionName = "Jobs:CnjCapture";

    public bool Enabled { get; init; } = true;

    /// <summary>Cron de 5 campos (Hangfire/NCrontab não suporta segundos). Padrão: 03:00 todos os dias.</summary>
    public string CronExpression { get; init; } = "0 3 * * *";

    /// <summary>
    /// Início fixo do intervalo de busca. Deixe nulo para usar a janela relativa
    /// (<see cref="LookbackDays"/>), que é o modo correto para execução recorrente — datas
    /// fixas existem para recarga histórica pontual.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>Fim fixo do intervalo de busca. Nulo = hoje.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>
    /// Quantos dias para trás buscar quando <see cref="StartDate"/> não é informado. Mais de
    /// um dia de propósito: publicações às vezes entram no diário com atraso e a janela
    /// sobreposta é barata — a deduplicação por ExternalId absorve a repetição (ADR-056).
    /// </summary>
    public int LookbackDays { get; init; } = 3;

    /// <summary>
    /// Siglas dos tribunais para a VARREDURA do diário por período.
    ///
    /// DESLIGADA por padrão (lista vazia) e assim deve permanecer no ambiente de um
    /// escritório: a varredura traz o diário inteiro do tribunal — dezenas de milhares de
    /// publicações de processos de terceiros, que não são do escritório e não deveriam
    /// entrar na base dele. A captura de produção é dirigida pelos processos CADASTRADOS
    /// (ver <see cref="CnjCaptureJob"/>).
    ///
    /// Existe apenas como ferramenta de diagnóstico, para verificar a integração com o CNJ
    /// sem depender de haver processo cadastrado.
    /// </summary>
    public IReadOnlyList<string> Tribunais { get; init; } = [];

    /// <summary>
    /// Números CNJ avulsos, somados aos processos cadastrados. Vazio no uso normal — os
    /// processos de interesse vêm do BANCO, não da configuração (ver
    /// IProcessoRepository.ListParaCapturaAsync).
    ///
    /// Serve para investigar um processo específico sem cadastrá-lo.
    /// </summary>
    public IReadOnlyList<string> CnjNumbers { get; init; } = [];

    /// <summary>
    /// Janela de datas aplicada também à captura por processo. Falso (padrão) traz o
    /// HISTÓRICO COMPLETO do processo — o comportamento certo na primeira captura de um
    /// processo recém-cadastrado, que precisa das publicações anteriores ao cadastro.
    /// </summary>
    public bool RestrictProcessCaptureToWindow { get; init; }

    public int PageSize { get; init; } = 100;

    /// <summary>
    /// Quantos alvos (processos) são consultados antes de uma pausa. Junto com
    /// <see cref="DelayBetweenBatchesMs"/>, espaça as chamadas ao CNJ: o rate limit é por IP
    /// e um escritório com centenas de processos o estouraria numa rajada só.
    /// </summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>Pausa entre lotes, em milissegundos. Zero desliga a pausa.</summary>
    public int DelayBetweenBatchesMs { get; init; } = 1000;

    /// <summary>
    /// Teto de páginas por tribunal em uma execução, para o job não rodar indefinidamente se
    /// o filtro estiver amplo demais. Ao atingir o teto, o job loga um aviso — sinal de que a
    /// janela ou a lista de tribunais precisa ser ajustada.
    /// </summary>
    public int MaxPages { get; init; } = 50;

    /// <summary>
    /// Restringe a captura a estes escritórios. VAZIO (o padrão) significa TODOS os que têm
    /// processo ativo cadastrado — que é o comportamento correto em produção: cada escritório
    /// recebe as publicações dos seus processos, sem ninguém precisar listá-lo aqui.
    ///
    /// Preencher só faz sentido para depurar um tenant específico.
    /// </summary>
    public IReadOnlyList<Guid> TenantIds { get; init; } = [];
}
