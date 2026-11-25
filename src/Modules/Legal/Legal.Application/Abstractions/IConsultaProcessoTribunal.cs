using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Porta de consulta de METADADOS de um processo no tribunal, usada para auto-preencher o
/// cadastro. A implementação registrada é um composto: o juscraper (portais dos tribunais) é a
/// fonte principal e o DataJud, o fallback (ver ADR-075 e ADR-064).
///
/// A porta é do módulo Legal, e não do módulo de integração, justamente porque a fonte é
/// provisória: o DataJud proíbe uso comercial, e a ADR-064 já registra que trocar de
/// provedor é cenário previsto. Com a porta aqui, trocar significa escrever outro
/// adaptador — o handler, o endpoint e a tela não mudam.
///
/// Não confundir com a captura de PUBLICAÇÕES (DJEN), que é outra API e outro fluxo.
/// </summary>
public interface IConsultaProcessoTribunal
{
    /// <summary>
    /// Consulta o processo. Sucesso com <c>null</c> significa "consultei e não existe" —
    /// desfecho normal, não falha: nem todo tribunal envia todos os processos.
    /// </summary>
    Task<Result<ProcessoTribunal?>> ConsultarAsync(CNJNumber cnjNumber, CancellationToken cancellationToken = default);
}

/// <param name="NumeroProcesso">Número com máscara, na forma canônica.</param>
/// <param name="Tribunal">Sigla do tribunal.</param>
/// <param name="Grau">Grau de jurisdição (G1, G2, JE…), quando informado.</param>
/// <param name="DataAjuizamento">Data de distribuição, quando informada.</param>
/// <param name="EmSegredoDeJustica">Quando verdadeiro, os metadados podem vir incompletos.</param>
/// <param name="Classe">Classe processual da tabela unificada do CNJ.</param>
/// <param name="Sistema">Sistema de origem (PJe, Projudi…).</param>
/// <param name="OrgaoJulgador">Vara ou órgão responsável.</param>
/// <param name="Assuntos">Assuntos da tabela unificada; o primeiro é o principal.</param>
/// <param name="Movimentos">Andamentos, do mais recente para o mais antigo.</param>
/// <param name="CodigoMunicipioIbge">Município do órgão julgador, quando o tribunal informa.</param>
/// <param name="Fonte">De onde vieram os dados: <see cref="FonteConsulta.Juscraper"/> ou <see cref="FonteConsulta.DataJud"/>.</param>
public sealed record ProcessoTribunal(
    string NumeroProcesso,
    string Tribunal,
    string? Grau,
    DateOnly? DataAjuizamento,
    bool EmSegredoDeJustica,
    string? Classe,
    string? Sistema,
    string? OrgaoJulgador,
    IReadOnlyList<string> Assuntos,
    IReadOnlyList<MovimentoProcesso> Movimentos,
    int? CodigoMunicipioIbge = null,
    string? Fonte = null);

/// <summary>Nomes estáveis das fontes de consulta (vão para a resposta da API e para métricas).</summary>
public static class FonteConsulta
{
    /// <summary>Serviço juscraper: consulta direta aos portais públicos dos tribunais (e-SAJ, PJe).</summary>
    public const string Juscraper = "juscraper";

    /// <summary>API Pública do DataJud (CNJ). Fallback — uso restrito a portfólio (ADR-064).</summary>
    public const string DataJud = "datajud";
}

/// <param name="Nome">Descrição do movimento.</param>
/// <param name="DataHora">Quando ocorreu.</param>
public sealed record MovimentoProcesso(string Nome, DateTimeOffset? DataHora);
