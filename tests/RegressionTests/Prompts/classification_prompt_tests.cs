using FluentAssertions;

namespace Advocacia.RegressionTests.Prompts;

/// <summary>
/// Regressão do prompt classification.md: 10 publicações, cada uma com o tipo de ato esperado.
/// Para cada uma verifica (1) que a resposta passou na validação do contrato JSON — o serviço
/// só devolve sucesso se o JSON é válido e segue o contrato —, (2) que o act_type é o esperado e
/// (3) que a confiança é maior que zero.
///
/// ATENÇÃO sobre os textos: são SINTÉTICOS, escritos no formato e no vocabulário das
/// comunicações do DJEN, com partes, números de processo e órgãos fictícios. Não são extratos de
/// publicações reais — o repositório não deve receber conteúdo processual de clientes, nem
/// anonimizado de forma insuficiente. Para ampliar a cobertura com casos reais do escritório,
/// anonimizar (nomes, CPF/CNPJ, números CNJ, endereços) ANTES de colar aqui.
///
/// Chama a API real (custo): trait PromptRegression, fora do CI, pulado sem ANTHROPIC_API_KEY.
///   dotnet test --filter Category=PromptRegression
/// </summary>
[Trait("Category", PromptRegressionHarness.Category)]
public class ClassificationPromptTests
{
    public static TheoryData<string, string, string, string, string> Publications() => new()
    {
        {
            "despacho_de_mero_expediente",
            "PODER JUDICIÁRIO DO ESTADO DO CEARÁ. 3ª Vara Cível da Comarca de Fortaleza. Processo nº 0000001-01.2025.8.06.0001. DESPACHO. Vistos. Junte-se a petição protocolada sob o nº 1234. Após, voltem os autos conclusos. Expedientes necessários. Fortaleza, 10 de março de 2025. Juiz de Direito.",
            "TJCE", "Civel", "despacho"
        },
        {
            "sentenca_de_procedencia",
            "PODER JUDICIÁRIO. 2ª Vara Cível. Processo nº 0000002-02.2025.8.06.0001. SENTENÇA. Vistos etc. Trata-se de ação de cobrança ajuizada por EMPRESA ALFA LTDA em face de FULANO DE TAL. Pelo exposto, JULGO PROCEDENTE o pedido, com resolução do mérito, nos termos do art. 487, I, do Código de Processo Civil, para condenar o réu ao pagamento de R$ 10.000,00, corrigidos monetariamente. Condeno o réu ao pagamento das custas e dos honorários advocatícios, fixados em 10% sobre o valor da condenação. Publique-se. Registre-se. Intimem-se.",
            "TJCE", "Civel", "sentenca"
        },
        {
            "acordao_de_camara",
            "PODER JUDICIÁRIO DO ESTADO DO CEARÁ. TRIBUNAL DE JUSTIÇA. 2ª Câmara de Direito Privado. Apelação Cível nº 0000003-03.2024.8.06.0001. ACÓRDÃO. Acordam os Desembargadores integrantes da Segunda Câmara de Direito Privado, por unanimidade, em conhecer da apelação para negar-lhe provimento, nos termos do voto do Relator, que fica fazendo parte integrante deste julgado. Fortaleza, 12 de fevereiro de 2025. Desembargador Relator.",
            "TJCE", "Civel", "acordao"
        },
        {
            "decisao_interlocutoria_tutela",
            "PODER JUDICIÁRIO. 5ª Vara Cível. Processo nº 0000004-04.2025.8.06.0001. DECISÃO. Trata-se de pedido de tutela de urgência formulado por BETA COMÉRCIO LTDA, que alega perigo de dano irreparável. Presentes a probabilidade do direito e o perigo de dano (art. 300 do CPC), DEFIRO a tutela de urgência para determinar que a ré se abstenha de realizar a negativação do nome da autora até ulterior deliberação. Cite-se a ré. Cumpra-se.",
            "TJCE", "Consumidor", "decisao_interlocutoria"
        },
        {
            "intimacao_para_replica",
            "PODER JUDICIÁRIO. 1ª Vara Cível. Processo nº 0000005-05.2025.8.06.0001. ATO ORDINATÓRIO. Fica a parte autora, por seu advogado, intimada para, no prazo de 15 (quinze) dias, manifestar-se em réplica sobre a contestação e os documentos que a acompanham, nos termos dos arts. 350 e 351 do CPC.",
            "TJCE", "Civel", "intimacao"
        },
        {
            "citacao_para_contestar",
            "PODER JUDICIÁRIO. 4ª Vara Cível. Processo nº 0000006-06.2025.8.06.0001. CITAÇÃO. Fica o réu SICRANO DE TAL citado dos termos da ação proposta, para, querendo, apresentar contestação no prazo de 15 (quinze) dias úteis, sob pena de revelia e presunção de veracidade dos fatos alegados na petição inicial (arts. 335 e 344 do CPC), cuja cópia segue anexa.",
            "TJCE", "Civel", "citacao"
        },
        {
            "designacao_de_audiencia",
            "PODER JUDICIÁRIO. 1ª Vara do Trabalho de Fortaleza. Processo nº 0000007-07.2025.5.07.0001. Fica designada AUDIÊNCIA UNA para o dia 20 de maio de 2025, às 09h30min, a realizar-se na sala de audiências desta Vara. Ficam as partes intimadas, devendo comparecer acompanhadas de suas testemunhas, independentemente de intimação.",
            "TRT7", "Trabalhista", "audiencia"
        },
        {
            "edital_de_citacao",
            "EDITAL DE CITAÇÃO. PRAZO DE 20 (VINTE) DIAS. Processo nº 0000008-08.2025.8.06.0001. O Juiz de Direito da 6ª Vara Cível, faz saber aos que o presente edital virem, ou dele conhecimento tiverem, que por este juízo e secretaria se processam os autos da ação de usucapião, em que é autor GAMA EMPREENDIMENTOS LTDA, sendo citados os réus em lugar incerto e eventuais interessados, para contestarem no prazo de 15 dias, após o decurso do prazo deste edital.",
            "TJCE", "Civel", "edital"
        },
        {
            "decisao_monocratica_do_relator",
            "PODER JUDICIÁRIO. TRIBUNAL DE JUSTIÇA. Agravo de Instrumento nº 0000009-09.2025.8.06.0000. DECISÃO MONOCRÁTICA. Trata-se de agravo de instrumento interposto contra decisão que indeferiu a gratuidade da justiça. O recurso é manifestamente inadmissível, por ausência de peça obrigatória (art. 1.017, I, do CPC). Diante do exposto, NÃO CONHEÇO do recurso, com fundamento no art. 932, III, do CPC. Publique-se. Intimem-se. Relator.",
            "TJCE", "Civel", "decisao_monocratica"
        },
        {
            "comunicado_sem_ato_decisorio",
            "PODER JUDICIÁRIO. Secretaria da 2ª Vara Cível. Processo nº 0000010-10.2025.8.06.0001. CERTIDÃO. Certifico que, em cumprimento ao despacho anterior, procedi à alteração da classe processual deste feito para Cumprimento de Sentença. Nada mais. Fortaleza, 3 de abril de 2025. Servidor.",
            "TJCE", "Civel", "outros,despacho"
        },
    };

    [PromptTheory]
    [MemberData(nameof(Publications))]
    public async Task Classify_ReturnsAValidContractWithTheExpectedActType(
        string caseName, string content, string tribunal, string area, string acceptableActTypes)
    {
        var result = await PromptRegressionHarness.Classification.ClassifyAsync(content, tribunal, area);

        result.IsSuccess.Should().BeTrue($"[{caseName}] a resposta deve ser um JSON válido que segue o contrato (erro: {(result.IsFailure ? result.Error.Code : "-")})");
        acceptableActTypes.Split(',').Should().Contain(result.Value.ActType, $"[{caseName}] tipo de ato");
        result.Value.Confidence.Should().BeGreaterThan(0, $"[{caseName}] confiança");
    }
}
