namespace Advocacia.Modules.Financeiro.Application.Features.Lancamentos;

/// <summary>
/// "Hoje" e "mês corrente" no horário de Brasília. O Brasil não tem horário de verão desde 2019, então
/// um deslocamento fixo de −3 h é exato e não depende de o servidor ter o banco de fusos instalado.
/// Sem isso, um honorário pago às 22 h do último dia do mês cairia no mês seguinte (UTC).
/// </summary>
internal static class RelogioBrasil
{
    internal static readonly TimeSpan Deslocamento = TimeSpan.FromHours(-3);

    internal static DateTimeOffset EmBrasilia(DateTimeOffset instante) => instante.ToOffset(Deslocamento);

    internal static DateOnly Hoje(DateTimeOffset agora) => DateOnly.FromDateTime(EmBrasilia(agora).DateTime);

    /// <summary>
    /// Primeiro instante do mês (em Brasília) em que <paramref name="agora"/> cai, EXPRESSO EM UTC: o Npgsql
    /// só grava/compara <c>timestamptz</c> com offset 0 e recusa qualquer outro. É o mesmo instante —
    /// 00:00 de Brasília é 03:00 UTC.
    /// </summary>
    internal static DateTimeOffset InicioDoMes(DateTimeOffset agora)
    {
        var local = EmBrasilia(agora);

        return new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, Deslocamento).ToUniversalTime();
    }

    /// <summary>Soma meses a um início de mês. Sem horário de verão, o deslocamento fixo mantém o dia 1 às 03:00 UTC.</summary>
    internal static DateTimeOffset AddMeses(DateTimeOffset inicioDeMes, int meses) => inicioDeMes.AddMonths(meses);
}
