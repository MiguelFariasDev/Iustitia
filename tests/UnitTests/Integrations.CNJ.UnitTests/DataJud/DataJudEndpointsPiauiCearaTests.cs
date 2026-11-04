using Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;
using FluentAssertions;
using Xunit;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.DataJud;

/// <summary>
/// Todo tribunal com jurisdição no Piauí e no Ceará precisa ter índice no DataJud: justiça
/// estadual, federal (TRF5 cobre os dois estados), trabalhista e eleitoral.
/// </summary>
public class DataJudEndpointsPiauiCearaTests
{
    [Theory]
    [InlineData("TJPI", "/api_publica_tjpi/_search")]
    [InlineData("TJCE", "/api_publica_tjce/_search")]
    [InlineData("TRF5", "/api_publica_trf5/_search")]
    [InlineData("TRT22", "/api_publica_trt22/_search")]
    [InlineData("TRT7", "/api_publica_trt7/_search")]
    [InlineData("TRE-PI", "/api_publica_tre-pi/_search")]
    [InlineData("TRE-CE", "/api_publica_tre-ce/_search")]
    public void Tribunais_do_Piaui_e_do_Ceara_tem_indice_no_DataJud(string tribunal, string caminho) =>
        DataJudEndpoints.GetSearchPath(tribunal).Should().Be(caminho);
}
