using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Domain.Consulta;

/// <summary>
/// A sessão de verificação de identidade que antecede a consulta de andamento pelo bot. Liga UM
/// atendimento a UM cliente cadastrado do escritório e guarda só o necessário: nunca o documento
/// informado (nem o certo, nem os errados) — a comparação acontece e o valor é descartado.
///
/// Regras:
/// <list type="bullet">
/// <item>vale <see cref="SessionLifetime"/> a partir de quando começou, e o tempo NÃO se estende ao verificar;</item>
/// <item><see cref="MaxFailedAttempts"/> falhas bloqueiam a verificação do cliente por <see cref="BlockDuration"/>;</item>
/// <item>recomeçar a sessão NÃO zera as falhas: quem erra duas vezes, recomeça e erra de novo é bloqueado
/// do mesmo jeito (ver <see cref="Start"/>) — senão o limite seria contornável com "iniciar" em laço;</item>
/// <item>os processos liberados ficam FIXADOS na verificação, para o índice que o cliente escolhe
/// continuar apontando para o mesmo processo.</item>
/// </list>
/// </summary>
public sealed class ConsultaSessao : Entity<Guid>, IHasTenant
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(30);

    /// <summary>Janela em que falhas de sessões anteriores do mesmo cliente ainda contam.</summary>
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Por quanto tempo, depois de uma tentativa, a mensagem do cliente ainda é tratada como
    /// possivelmente conter o documento. O Bot registra a mensagem no histórico antes OU depois de
    /// chamar a verificação, e o documento não pode ficar no histórico em nenhum dos dois casos.
    /// </summary>
    public static readonly TimeSpan RedactionWindow = TimeSpan.FromSeconds(60);

    public const int MaxFailedAttempts = 3;

    /// <summary>O menu do WhatsApp não comporta mais que isto.</summary>
    public const int MaxProcesses = 10;

    public Guid TenantId { get; private set; }

    public AtendimentoId AtendimentoId { get; private set; }

    /// <summary>O cliente (de Legal) dono do telefone, resolvido ao iniciar.</summary>
    public Guid ClientId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset? BlockedUntil { get; private set; }

    /// <summary>Os processos liberados pela verificação, em ordem; o índice 1 do cliente é o primeiro.</summary>
    public List<Guid> ProcessIds { get; private set; } = [];

    private ConsultaSessao()
    {
        // EF Core.
    }

    private ConsultaSessao(
        Guid tenantId, AtendimentoId atendimentoId, Guid clientId, int failedAttempts, DateTimeOffset now)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        AtendimentoId = atendimentoId;
        ClientId = clientId;
        StartedAt = now;
        ExpiresAt = now + SessionLifetime;
        FailedAttempts = failedAttempts;
    }

    /// <param name="inheritedFailures">
    /// O maior número de falhas, dentro de <see cref="FailureWindow"/>, de sessões anteriores deste
    /// cliente que não terminaram em verificação nem em bloqueio já cumprido.
    /// </param>
    public static ConsultaSessao Start(
        Guid tenantId, AtendimentoId atendimentoId, Guid clientId, int inheritedFailures, DateTimeOffset now) =>
        new(tenantId, atendimentoId, clientId, Math.Clamp(inheritedFailures, 0, MaxFailedAttempts - 1), now);

    public ConsultaEtapa Etapa(DateTimeOffset now)
    {
        if (BlockedUntil is { } blockedUntil && now < blockedUntil)
        {
            return ConsultaEtapa.Bloqueado;
        }

        if (now >= ExpiresAt)
        {
            return ConsultaEtapa.Expirada;
        }

        return VerifiedAt is null ? ConsultaEtapa.AguardandoDocumento : ConsultaEtapa.Verificado;
    }

    /// <summary>
    /// Registra o resultado da comparação do documento. Só muda o estado se a sessão está aguardando
    /// o documento; em qualquer outro estado devolve a etapa atual sem contar nada — bloqueado
    /// continua bloqueado, expirada continua expirada, verificada continua verificada.
    /// </summary>
    public ConsultaEtapa RegisterAttempt(bool documentMatches, DateTimeOffset now)
    {
        var current = Etapa(now);
        if (current != ConsultaEtapa.AguardandoDocumento)
        {
            return current;
        }

        LastAttemptAt = now;

        if (documentMatches)
        {
            VerifiedAt = now;
            FailedAttempts = 0;
            return ConsultaEtapa.Verificado;
        }

        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts)
        {
            BlockedUntil = now + BlockDuration;
            return ConsultaEtapa.Bloqueado;
        }

        return ConsultaEtapa.Negado;
    }

    /// <summary>Fixa os processos que a verificação liberou (no máximo <see cref="MaxProcesses"/>).</summary>
    public void AttachProcesses(IEnumerable<Guid> processIds) =>
        ProcessIds = processIds.Distinct().Take(MaxProcesses).ToList();

    /// <summary>Encerra a sessão ao recomeçar a verificação. Não desfaz bloqueio nem apaga falhas.</summary>
    public void Invalidate(DateTimeOffset now)
    {
        if (ExpiresAt > now)
        {
            ExpiresAt = now;
        }
    }

    /// <summary>
    /// Se uma mensagem do cliente recebida agora pode conter o documento e deve ser gravada no
    /// histórico sem o texto: enquanto se aguarda o documento, ou logo depois de uma tentativa.
    /// </summary>
    public bool ShouldRedactInbound(DateTimeOffset now) =>
        Etapa(now) == ConsultaEtapa.AguardandoDocumento
        || (LastAttemptAt is { } last && now - last < RedactionWindow);
}
