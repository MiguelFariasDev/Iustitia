using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

/// <summary>
/// Credencial server-to-server do Bot. Existe UMA ativa por escritório em uso normal (a
/// rotação emite a nova e revoga a antiga), e é ela que diz ao Core de qual escritório vem
/// a chamada — o RLS precisa do tenant em toda consulta.
///
/// Só o hash SHA-256 é guardado: a chave em claro aparece uma única vez, na emissão. A chave
/// tem 256 bits aleatórios, então um hash rápido basta (não é senha escolhida por humano).
/// </summary>
public sealed class BotApiKey : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Primeiros caracteres da chave, só para o operador reconhecê-la numa listagem.</summary>
    public string Prefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    private BotApiKey()
    {
        // EF Core.
    }

    private BotApiKey(
        Guid tenantId, string name, string prefix, string keyHash, Guid? createdByUserId, DateTimeOffset createdAt)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Name = name;
        Prefix = prefix;
        KeyHash = keyHash;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public static BotApiKey Issue(
        Guid tenantId, string name, string prefix, string keyHash, Guid? createdByUserId, DateTimeOffset now) =>
        new(tenantId, name.Trim(), prefix, keyHash, createdByUserId, now);

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now;
}
