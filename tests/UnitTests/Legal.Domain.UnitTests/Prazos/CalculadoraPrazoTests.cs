using Advocacia.Modules.Legal.Domain.Prazos;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Prazos;

public class CalendarioForenseTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Pascoa_BateComOsValoresConhecidos(int ano, int mes, int dia) =>
        CalendarioForense.Pascoa(ano).Should().Be(new DateOnly(ano, mes, dia));

    [Fact]
    public void FeriadosNacionais_IncluemSextaDaPaixao_EConscienciaNegraSoDesde2024()
    {
        CalendarioForense.FeriadosNacionais(2026).Should().Contain(new DateOnly(2026, 4, 3));
        CalendarioForense.FeriadosNacionais(2026).Should().Contain(new DateOnly(2026, 11, 20));
        CalendarioForense.FeriadosNacionais(2023).Should().NotContain(new DateOnly(2023, 11, 20));
        CalendarioForense.FeriadosNacionais(2026).Should().Contain([
            new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 21), new DateOnly(2026, 5, 1), new DateOnly(2026, 9, 7),
            new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15), new DateOnly(2026, 12, 25),
        ]);
    }

    [Theory]
    [InlineData(2026, 12, 19, false)]
    [InlineData(2026, 12, 20, true)]
    [InlineData(2027, 1, 20, true)]
    [InlineData(2027, 1, 21, false)]
    public void Recesso_VaiDe20DeDezembroA20DeJaneiro(int ano, int mes, int dia, bool esperado) =>
        CalendarioForense.EmRecesso(new DateOnly(ano, mes, dia)).Should().Be(esperado);

    [Fact]
    public void DiaUtil_ConsideraFimDeSemanaFeriadoNacionalELocal()
    {
        var local = new HashSet<DateOnly> { new(2026, 3, 19) }; // data estadual hipotética
        CalendarioForense.DiaUtil(new DateOnly(2026, 11, 23)).Should().BeTrue();
        CalendarioForense.DiaUtil(new DateOnly(2026, 11, 21)).Should().BeFalse("sábado");
        CalendarioForense.DiaUtil(new DateOnly(2026, 11, 20)).Should().BeFalse("feriado nacional");
        CalendarioForense.DiaUtil(new DateOnly(2026, 3, 19)).Should().BeTrue();
        CalendarioForense.DiaUtil(new DateOnly(2026, 3, 19), local).Should().BeFalse("feriado local informado");
    }
}

public class CalculadoraPrazoTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    [Fact]
    public void QuinzeDiasUteis_PulamFeriadoNacionalDe20DeNovembro()
    {
        // Ciência numa segunda (16/11/2026): dia 1 = 17/11. A sexta 20/11 é feriado e não conta.
        var resultado = CalculadoraPrazo.Calcular(D(2026, 11, 16), 15);

        resultado.InicioDaContagem.Should().Be(D(2026, 11, 17));
        resultado.Vencimento.Should().Be(D(2026, 12, 8));
        resultado.DiasContados.Should().Be(15);
        resultado.DiasDeRecessoIgnorados.Should().Be(0);
    }

    [Fact]
    public void Disponibilizacao_PublicaNoPrimeiroDiaUtil_EComecaDepois()
    {
        // Disponibilizado na sexta 13/11: publicação segunda 16/11; contagem começa terça 17/11.
        var resultado = CalculadoraPrazo.Calcular(D(2026, 11, 13), 15, referencia: ReferenciaDoPrazo.Disponibilizacao);

        resultado.DataDaPublicacao.Should().Be(D(2026, 11, 16));
        resultado.InicioDaContagem.Should().Be(D(2026, 11, 17));
        resultado.Vencimento.Should().Be(D(2026, 12, 8));
        resultado.Observacoes.Should().Contain(o => o.Contains("Publicação considerada em 16/11/2026"));
    }

    [Fact]
    public void RecessoForense_SuspendeAContagemEORetomaEm21DeJaneiro()
    {
        // Ciência quinta 10/12/2026, 10 dias úteis: 6 antes do recesso (11 a 18/12), 4 depois (21, 22, 25 e 26/01).
        var resultado = CalculadoraPrazo.Calcular(D(2026, 12, 10), 10);

        resultado.Vencimento.Should().Be(D(2027, 1, 26));
        resultado.DiasDeRecessoIgnorados.Should().BeGreaterThan(0);
        resultado.Observacoes.Should().Contain(o => o.Contains("recesso forense"));
    }

    [Fact]
    public void SextaFeiraSanta_NaoContaComoDiaUtil()
    {
        // Quarta 01/04/2026: 02/04 (1), 03/04 Sexta da Paixão (não), 06/04 (2), 07/04 (3).
        CalculadoraPrazo.Calcular(D(2026, 4, 1), 3).Vencimento.Should().Be(D(2026, 4, 7));
    }

    [Fact]
    public void DiasCorridos_ComecamNoPrimeiroDiaUtil_EContamFinaisDeSemana()
    {
        // Ciência na sexta 20/11 (feriado): começa segunda 23/11; 5 corridos → sexta 27/11.
        CalculadoraPrazo.Calcular(D(2026, 11, 20), 5, TipoContagem.DiasCorridos).Vencimento.Should().Be(D(2026, 11, 27));
    }

    [Fact]
    public void DiasCorridos_VencimentoNoDomingo_ProrrogaParaSegunda()
    {
        // Quinta 26/11: começa sexta 27/11; 3 corridos terminam no domingo 29/11 → prorroga para segunda 30/11.
        var resultado = CalculadoraPrazo.Calcular(D(2026, 11, 26), 3, TipoContagem.DiasCorridos);

        resultado.Vencimento.Should().Be(D(2026, 11, 30));
        resultado.Observacoes.Should().Contain(o => o.Contains("prorrogado") && o.Contains("art. 224 §1º"));
    }

    [Fact]
    public void FeriadoLocalInformado_NaoContaComoDiaUtil()
    {
        var sem = CalculadoraPrazo.Calcular(D(2026, 3, 16), 5);
        var com = CalculadoraPrazo.Calcular(D(2026, 3, 16), 5, feriadosLocais: new HashSet<DateOnly> { D(2026, 3, 19) });

        sem.Vencimento.Should().Be(D(2026, 3, 23));
        com.Vencimento.Should().Be(D(2026, 3, 24));
    }

    [Fact]
    public void SempreAvisaQueEEstimativa() =>
        CalculadoraPrazo.Calcular(D(2026, 11, 16), 5).Observacoes.Should().Contain(o => o.Contains("Estimativa"));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1001)]
    public void DiasForaDoLimite_Falha(int dias)
    {
        var act = () => CalculadoraPrazo.Calcular(D(2026, 11, 16), dias);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void QualquerDataBase_NuncaVenceEmDiaSemExpedienteNemNoRecesso()
    {
        var inicio = D(2026, 1, 1);
        for (var i = 0; i < 400; i++)
        {
            var dataBase = inicio.AddDays(i);
            foreach (var tipo in new[] { TipoContagem.DiasUteis, TipoContagem.DiasCorridos })
            {
                var v = CalculadoraPrazo.Calcular(dataBase, 15, tipo).Vencimento;
                CalendarioForense.DiaUtil(v).Should().BeTrue($"{dataBase} ({tipo})");
                CalendarioForense.EmRecesso(v).Should().BeFalse($"{dataBase} ({tipo})");
                v.Should().BeAfter(dataBase);
            }
        }
    }
}
