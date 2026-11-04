using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Shared;

/// <summary>
/// A sigla do tribunal sai do próprio número CNJ (campos J e TR da Resolução 65/2008). É o
/// que decide em qual índice do DataJud a consulta vai bater — errar aqui devolve "processo
/// não encontrado" num processo que existe, sem erro nenhum para denunciar a causa.
/// </summary>
public class TribunalResolverTests
{
    [Theory]
    [InlineData(1, "00", "STF")]
    [InlineData(3, "00", "STJ")]
    [InlineData(7, "00", "STM")]
    [InlineData(6, "00", "TSE")]
    public void Resolve_Superiores(int segmento, string tribunal, string esperado) =>
        TribunalResolver.Resolve(segmento, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData("01", "TRF1")]
    [InlineData("06", "TRF6")]
    public void Resolve_JusticaFederal(string tribunal, string esperado) =>
        TribunalResolver.Resolve(4, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData(4, "05", "TRF5")]
    [InlineData(5, "07", "TRT7")]
    [InlineData(5, "22", "TRT22")]
    [InlineData(8, "06", "TJCE")]
    [InlineData(8, "18", "TJPI")]
    [InlineData(6, "06", "TRE-CE")]
    [InlineData(6, "18", "TRE-PI")]
    public void Resolve_Piaui_e_Ceara(int segmento, string tribunal, string esperado) =>
        TribunalResolver.Resolve(segmento, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData("01", "TRT1")]
    [InlineData("07", "TRT7")]
    [InlineData("24", "TRT24")]
    public void Resolve_JusticaDoTrabalho(string tribunal, string esperado) =>
        TribunalResolver.Resolve(5, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData("01", "TJAC")]
    [InlineData("06", "TJCE")]
    [InlineData("07", "TJDFT")]
    [InlineData("19", "TJRJ")]
    [InlineData("26", "TJSP")]
    [InlineData("27", "TJTO")]
    public void Resolve_JusticaEstadual(string tribunal, string esperado) =>
        TribunalResolver.Resolve(8, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData("06", "TRE-CE")]
    [InlineData("26", "TRE-SP")]
    public void Resolve_JusticaEleitoral(string tribunal, string esperado) =>
        TribunalResolver.Resolve(6, tribunal).Should().Be(esperado);

    [Theory]
    [InlineData("13", "TJMMG")]
    [InlineData("21", "TJMRS")]
    [InlineData("26", "TJMSP")]
    public void Resolve_JusticaMilitarEstadual(string tribunal, string esperado) =>
        TribunalResolver.Resolve(9, tribunal).Should().Be(esperado);

    /// <summary>
    /// Justiça Militar Estadual existe em três estados (art. 125, §3º, CF). Um código de UF
    /// válido em outro segmento não cria um tribunal militar que não existe.
    /// </summary>
    [Fact]
    public void Resolve_MilitarEstadualForaDosTresEstados_RetornaNulo() =>
        TribunalResolver.Resolve(9, "06").Should().BeNull();

    [Theory]
    [InlineData(4, "07")]   // só existem TRF1..TRF6
    [InlineData(5, "25")]   // só existem TRT1..TRT24
    [InlineData(8, "40")]   // não há UF 40
    [InlineData(0, "26")]   // segmento 0 não existe
    public void Resolve_CombinacaoInexistente_RetornaNulo(int segmento, string tribunal) =>
        TribunalResolver.Resolve(segmento, tribunal).Should().BeNull();

    [Fact]
    public void CNJNumber_ExpoeOTribunalDerivado()
    {
        var numero = CNJNumber.Create("0000832-35.2018.4.01.3202");

        numero.IsSuccess.Should().BeTrue();
        numero.Value.Tribunal.Should().Be("TRF1");
    }
}
