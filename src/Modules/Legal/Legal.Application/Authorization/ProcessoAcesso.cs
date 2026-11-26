using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;

namespace Advocacia.Modules.Legal.Application.Authorization;

/// <summary>
/// Regra de visibilidade de processo dentro do escritório. Processo comum é visível a quem tem
/// acesso ao módulo; processo CONFIDENCIAL só aos advogados do processo e à sociedade (Owner/Partner).
/// Quem não pode ver recebe "não encontrado" (nunca "proibido"), para não confirmar que o
/// processo existe.
/// </summary>
internal static class ProcessoAcesso
{
    internal static bool PodeVer(Processo processo, ICurrentUser user) =>
        !processo.Confidencial || EhSociedade(user) || (user.UserId is { } id && processo.TemAdvogado(id));

    /// <summary>
    /// Para listagens: o id do usuário a restringir, ou null quando ele vê tudo (sociedade).
    /// Sem usuário identificado, usa Guid.Empty — que não é responsável de nenhum processo.
    /// </summary>
    internal static Guid? RestricaoDeListagem(ICurrentUser user) =>
        EhSociedade(user) ? null : user.UserId ?? Guid.Empty;

    private static bool EhSociedade(ICurrentUser user) => user.IsInRole("Owner") || user.IsInRole("Partner");
}
