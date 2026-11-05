using Advocacia.Modules.Legal.Domain.Suggestions;
using FluentAssertions;

namespace Advocacia.RegressionTests.Prompts;

/// <summary>
/// Regressão do prompt deadline.md: 10 publicações com prazo conhecido (fixado no texto ou
/// previsto em lei). Verifica o número de dias e a forma de contagem sugeridos.
///
/// Os textos são SINTÉTICOS (ver a observação em <see cref="ClassificationPromptTests"/>). Os
/// prazos esperados vêm do próprio texto ou da lei: CPC arts. 219 (dias úteis), 321, 335, 523,
/// 1.003 §5º, 1.021 §2º; CPP art. 396 (prazos do processo penal são contínuos, art. 798).
///
/// Um caso de PRAZO EM HORAS confirma que o modelo não converte horas em dias (o contrato só
/// aceita dias) e responde sem prazo.
///
/// Chama a API real (custo): trait PromptRegression, fora do CI, pulado sem ANTHROPIC_API_KEY.
/// </summary>
[Trait("Category", PromptRegressionHarness.Category)]
public class DeadlinePromptTests
{
    public static TheoryData<string, string, string, int?, string?> Publications() => new()
    {
        {
            "contestacao_15_dias_uteis",
            "CITAÇÃO. Fica o réu citado para, querendo, apresentar contestação no prazo de 15 (quinze) dias, sob pena de revelia (arts. 335 e 344 do CPC).",
            "citacao", 15, "business"
        },
        {
            "emenda_da_inicial_15_dias",
            "DESPACHO. Intime-se a parte autora para, no prazo de 15 (quinze) dias, emendar a petição inicial, sanando os vícios apontados, sob pena de indeferimento (art. 321 do CPC).",
            "despacho", 15, "business"
        },
        {
            "cumprimento_voluntario_15_dias",
            "DECISÃO. Intime-se o executado, na pessoa de seu advogado, para pagar o débito no prazo de 15 (quinze) dias, acrescido de custas, se houver, sob pena de multa de 10% e honorários de 10% (art. 523 do CPC).",
            "decisao_interlocutoria", 15, "business"
        },
        {
            "contrarrazoes_agravo_interno_15_dias",
            "DESPACHO. Intime-se o agravado para responder ao agravo interno no prazo de 15 (quinze) dias (art. 1.021, § 2º, do CPC).",
            "despacho", 15, "business"
        },
        {
            "prazo_de_10_dias_no_texto",
            "ATO ORDINATÓRIO. Fica a parte ré intimada para, no prazo de 10 (dez) dias, apresentar os documentos requeridos pelo perito judicial.",
            "intimacao", 10, "business"
        },
        {
            "prazo_de_5_dias_no_texto",
            "INTIMAÇÃO. Fica a parte autora intimada para, no prazo de 5 (cinco) dias, manifestar-se sobre o laudo pericial juntado aos autos.",
            "intimacao", 5, "business"
        },
        {
            "embargos_de_declaracao_contrarrazoes_5_dias",
            "DESPACHO. Opostos embargos de declaração com pedido de efeito modificativo, intime-se a parte embargada para manifestação no prazo de 5 (cinco) dias (art. 1.023, § 2º, do CPC).",
            "despacho", 5, "business"
        },
        {
            "recurso_ordinario_trabalhista_8_dias",
            "1ª Vara do Trabalho. SENTENÇA publicada. Ficam as partes intimadas da sentença. Prazo para interposição de recurso ordinário: 8 (oito) dias (art. 895 da CLT).",
            "sentenca", 8, "business"
        },
        {
            "resposta_a_acusacao_penal_10_dias_corridos",
            "Vara Criminal. DESPACHO. Recebida a denúncia, cite-se o acusado e intime-se a defesa para apresentar resposta à acusação no prazo de 10 (dez) dias (art. 396 do Código de Processo Penal).",
            "despacho", 10, "calendar"
        },
        {
            "prazo_em_horas_nao_vira_dias",
            "DECISÃO. Intime-se o réu para cumprir a obrigação de fazer no prazo de 48 (quarenta e oito) horas, sob pena de multa diária.",
            "decisao_interlocutoria", null, null
        },
    };

    [PromptTheory]
    [MemberData(nameof(Publications))]
    public async Task Suggest_ReturnsTheKnownDeadline(
        string caseName, string content, string actType, int? expectedDays, string? expectedType)
    {
        var result = await PromptRegressionHarness.Deadline.SuggestAsync(content, actType);

        result.IsSuccess.Should().BeTrue($"[{caseName}] a resposta deve ser um JSON válido que segue o contrato (erro: {(result.IsFailure ? result.Error.Code : "-")})");
        result.Value.Confidence.Should().BeGreaterThan(0, $"[{caseName}] confiança");
        result.Value.Days.Should().Be(expectedDays, $"[{caseName}] dias do prazo");

        if (expectedType is not null)
        {
            result.Value.Type.Should().Be(
                DeadlineTypeExtensions.TryParse(expectedType, out var type) ? type : null,
                $"[{caseName}] forma de contagem");
        }
        else
        {
            result.Value.Type.Should().BeNull($"[{caseName}] sem prazo, sem forma de contagem");
        }
    }
}
