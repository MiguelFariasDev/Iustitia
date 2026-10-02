namespace Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;

/// <summary>
/// Metadados de um processo no DataJud, já traduzidos do formato Elasticsearch para o
/// vocabulário da aplicação. O que vem da API é envelope de busca (hits/_source); o que
/// circula daqui para dentro é este registro.
/// </summary>
/// <param name="NumeroProcesso">20 dígitos, sem máscara, como o DataJud devolve.</param>
/// <param name="Tribunal">Sigla informada pelo tribunal (ex.: "TRF1").</param>
/// <param name="Grau">Grau de jurisdição: G1, G2, JE, TR…</param>
/// <param name="DataAjuizamento">Data de distribuição/ajuizamento.</param>
/// <param name="NivelSigilo">0 = público. Valores maiores indicam restrição.</param>
/// <param name="Classe">Classe processual (tabela unificada do CNJ).</param>
/// <param name="Sistema">Sistema de origem (PJe, Projudi…), quando informado.</param>
/// <param name="Formato">Eletrônico ou físico, quando informado.</param>
/// <param name="OrgaoJulgador">Vara/órgão responsável, quando informado.</param>
/// <param name="Assuntos">Assuntos da tabela unificada; o primeiro costuma ser o principal.</param>
/// <param name="Movimentos">Andamentos, do mais recente para o mais antigo.</param>
public sealed record DataJudProcesso(
    string NumeroProcesso,
    string Tribunal,
    string? Grau,
    DateTimeOffset? DataAjuizamento,
    int NivelSigilo,
    DataJudItemTabelado? Classe,
    DataJudItemTabelado? Sistema,
    DataJudItemTabelado? Formato,
    DataJudOrgaoJulgador? OrgaoJulgador,
    IReadOnlyList<DataJudItemTabelado> Assuntos,
    IReadOnlyList<DataJudMovimento> Movimentos)
{
    /// <summary>Assunto principal — o primeiro da lista, que é como o CNJ a ordena.</summary>
    public DataJudItemTabelado? AssuntoPrincipal => Assuntos.Count > 0 ? Assuntos[0] : null;

    /// <summary>Processo sob segredo de justiça: os metadados podem vir incompletos.</summary>
    public bool EmSegredoDeJustica => NivelSigilo > 0;
}

/// <param name="Codigo">Código na tabela unificada do CNJ.</param>
/// <param name="Nome">Descrição legível.</param>
public sealed record DataJudItemTabelado(int Codigo, string Nome);

/// <param name="Codigo">Código do órgão no tribunal.</param>
/// <param name="Nome">Nome da vara/órgão.</param>
/// <param name="CodigoMunicipioIBGE">Município, quando o tribunal informa.</param>
public sealed record DataJudOrgaoJulgador(int Codigo, string Nome, int? CodigoMunicipioIBGE);

/// <param name="Codigo">Código do movimento na tabela unificada.</param>
/// <param name="Nome">Descrição do movimento.</param>
/// <param name="DataHora">Quando ocorreu.</param>
public sealed record DataJudMovimento(int Codigo, string Nome, DateTimeOffset? DataHora);
