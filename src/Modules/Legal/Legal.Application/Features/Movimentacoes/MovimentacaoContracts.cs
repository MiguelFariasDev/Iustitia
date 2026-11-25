using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.Movimentacoes;

/// <summary>
/// Busca os andamentos do processo no tribunal e grava os novos (ADR-075). Disparado pelo job
/// noturno (<paramref name="OrigemJob"/> = true, sem usuário) ou sob demanda pela tela.
/// </summary>
/// <param name="OrigemJob">
/// Verdadeiro só no job: dispensa a checagem de acesso a processo confidencial, que exige um
/// usuário. O endpoint sempre envia falso.
/// </param>
/// <param name="TrocarParaFonte">
/// Troca EXPLÍCITA de fonte ("juscraper"/"datajud"): grava uma nova base sem alertar nem avisar.
/// Nulo (o normal) mantém a fonte em que o processo já é acompanhado.
/// </param>
/// <param name="JanelaDias">Dias antes do último andamento capturado que a busca revisita.</param>
public sealed record SincronizarMovimentacoesCommand(
    Guid ProcessoId,
    bool OrigemJob = false,
    string? TrocarParaFonte = null,
    int JanelaDias = 3) : ICommand<SincronizarMovimentacoesResponse>;

/// <param name="Encontrado">Falso: a fonte respondeu e não conhece o processo.</param>
/// <param name="Fonte">Fonte que respondeu.</param>
/// <param name="Base">Primeira sincronização (ou troca de fonte): grava sem alertar nem avisar.</param>
/// <param name="Novos">Andamentos gravados agora.</param>
/// <param name="Alertou">Alerta in-app criado para o responsável.</param>
/// <param name="AvisoAtualizado">Rascunho de aviso ao cliente criado ou atualizado.</param>
public sealed record SincronizarMovimentacoesResponse(
    bool Encontrado,
    string? Fonte,
    bool Base,
    int Novos,
    bool Alertou,
    bool AvisoAtualizado);

public sealed record ListMovimentacoesQuery(Guid ProcessoId, int Page = 1, int PageSize = 20)
    : IQuery<ListMovimentacoesResponse>;

public sealed record MovimentacaoResponse(
    Guid Id,
    string Nome,
    string? Complemento,
    DateTimeOffset? DataHora,
    string? Instancia,
    bool PossuiDocumento,
    string Fonte,
    DateTimeOffset CapturadaEm);

public sealed record ListMovimentacoesResponse(
    IReadOnlyList<MovimentacaoResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
