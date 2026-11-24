namespace Advocacia.Modules.Legal.Domain.Prazos;

/// <summary>
/// Calendário forense NACIONAL: o que vale em qualquer tribunal. Feriados estaduais e municipais,
/// Corpus Christi, Carnaval, Semana Santa de cada tribunal e suspensões (greve, apagão do PJe,
/// ponto facultativo) variam por comarca e por ato do tribunal — por isso entram pelo chamador
/// (<c>feriadosLocais</c>) e o resultado SEMPRE é uma estimativa para o advogado conferir.
///
/// Fontes: feriados nacionais (Lei 662/1949 e alterações; Lei 10.607/2002 — Finados, Sexta-feira da Paixão;
/// Lei 14.759/2023 — Consciência Negra, nacional desde 2024) e recesso forense de 20/12 a 20/01 (CPC art. 220).
/// </summary>
public static class CalendarioForense
{
    /// <summary>Feriados nacionais que valem em todo ano: (mês, dia).</summary>
    private static readonly (int Mes, int Dia)[] FeriadosFixos =
    [
        (1, 1),   // Confraternização Universal
        (4, 21),  // Tiradentes
        (5, 1),   // Dia do Trabalho
        (9, 7),   // Independência
        (10, 12), // Nossa Senhora Aparecida
        (11, 2),  // Finados
        (11, 15), // Proclamação da República
        (12, 25), // Natal
    ];

    /// <summary>Primeiro ano em que 20/11 (Consciência Negra) é feriado NACIONAL.</summary>
    private const int PrimeiroAnoConscienciaNegra = 2024;

    public static IReadOnlySet<DateOnly> FeriadosNacionais(int ano)
    {
        var feriados = new HashSet<DateOnly>(FeriadosFixos.Select(f => new DateOnly(ano, f.Mes, f.Dia)))
        {
            Pascoa(ano).AddDays(-2), // Sexta-feira da Paixão
        };

        if (ano >= PrimeiroAnoConscienciaNegra)
        {
            feriados.Add(new DateOnly(ano, 11, 20));
        }

        return feriados;
    }

    /// <summary>Recesso forense (CPC art. 220): de 20 de dezembro a 20 de janeiro, inclusive. O prazo não corre.</summary>
    public static bool EmRecesso(DateOnly dia) =>
        (dia.Month == 12 && dia.Day >= 20) || (dia.Month == 1 && dia.Day <= 20);

    public static bool FimDeSemana(DateOnly dia) => dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool Feriado(DateOnly dia, IReadOnlySet<DateOnly>? feriadosLocais = null) =>
        FeriadosNacionais(dia.Year).Contains(dia) || feriadosLocais?.Contains(dia) == true;

    /// <summary>Dia de expediente: nem sábado, domingo ou feriado. (O recesso é tratado à parte: suspende a contagem.)</summary>
    public static bool DiaUtil(DateOnly dia, IReadOnlySet<DateOnly>? feriadosLocais = null) =>
        !FimDeSemana(dia) && !Feriado(dia, feriadosLocais);

    /// <summary>Próximo dia ÚTIL depois de <paramref name="dia"/> (o próprio dia não conta).</summary>
    public static DateOnly ProximoDiaUtil(DateOnly dia, IReadOnlySet<DateOnly>? feriadosLocais = null)
    {
        var candidato = dia.AddDays(1);
        while (!DiaUtil(candidato, feriadosLocais))
        {
            candidato = candidato.AddDays(1);
        }

        return candidato;
    }

    /// <summary>Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher, calendário gregoriano).</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var dia = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(ano, mes, dia);
    }
}
