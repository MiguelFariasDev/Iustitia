using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;

namespace Advocacia.Modules.Integrations.CNJ.Application.Abstractions;

/// <summary>
/// Porta de saída para a API Pública do DataJud (CNJ), que expõe os METADADOS do processo
/// — classe, assuntos, órgão julgador, movimentos. Não confundir com <see cref="ICnjClient"/>,
/// que fala com o DJEN e traz o TEXTO das publicações: são APIs diferentes, com endereços,
/// autenticação e formatos distintos.
///
/// USO RESTRITO A PORTFÓLIO. O Termo de Uso do DataJud proíbe uso comercial (cláusulas 3.3
/// e 3.8) e consumir a API implica aceitá-lo — ver docs/compliance/datajud-termo.md.
///
/// Falhas são devolvidas como <see cref="Result"/> de erro (faixa CNJ do catálogo), nunca
/// como exception: o consumidor é um formulário de cadastro, e não achar o processo é um
/// desfecho normal, não um defeito.
/// </summary>
public interface IDataJudClient
{
    Task<Result<DataJudProcesso?>> ConsultarProcessoAsync(
        string tribunal,
        string numeroProcesso,
        CancellationToken cancellationToken = default);
}
