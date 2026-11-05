using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Devices;

/// <summary>
/// Instalação do app móvel de um usuário: o endereço para onde o push é enviado e, ao mesmo
/// tempo, a unidade que o usuário pode revogar quando perde o celular (ADR-069).
///
/// Duas regras que protegem o acesso:
/// <list type="bullet">
/// <item><description>
/// <b>Revogar é permanente.</b> Re-registrar um token revogado NÃO o reativa: se reativasse, quem
/// roubou o aparelho (e ainda tem a sessão) desfaria a revogação só reabrindo o app. O usuário
/// legítimo volta com um token novo (reinstalar o app gera outro).
/// </description></item>
/// <item><description>
/// <b>O token é um segredo de entrega.</b> Não sai em nenhuma resposta da API, não é logado e
/// <see cref="ToString"/> não o inclui.
/// </description></item>
/// </list>
/// </summary>
public sealed class Device : AggregateRoot<DeviceId>, IHasTenant
{
    public const int TokenMinLength = 20;
    public const int TokenMaxLength = 1024;

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public DevicePlatform Platform { get; private set; }

    /// <summary>Token FCM/APNs. Nunca expor nem logar.</summary>
    public string DeviceToken { get; private set; } = null!;

    public bool IsRevoked { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Device()
    {
        // EF Core.
    }

    private Device(
        DeviceId id, Guid tenantId, Guid userId, DevicePlatform platform, string deviceToken, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Platform = platform;
        DeviceToken = deviceToken;
        LastSeenAt = now;
        CreatedAt = now;
    }

    public static Result<Device> Register(
        Guid tenantId, Guid userId, DevicePlatform platform, string? deviceToken, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Device>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<Device>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(UserId)));
        }

        var token = deviceToken?.Trim();
        if (!IsValidToken(token))
        {
            return Result.Failure<Device>(ErrorFactory.From(ErrorCode.DEVICE_TOKEN_INVALID));
        }

        return Result.Success(new Device(DeviceId.New(), tenantId, userId, platform, token!, now.ToUniversalTime()));
    }

    /// <summary>
    /// O mesmo app volta a se registrar (login, renovação): atualiza dono e plataforma. O dono pode
    /// mudar — um aparelho compartilhado em que outro usuário do escritório entrou. Um dispositivo
    /// revogado recusa, sem alterar nada (ver o resumo da classe).
    /// </summary>
    public Result Refresh(Guid userId, DevicePlatform platform, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DEVICE_REVOKED));
        }

        UserId = userId;
        Platform = platform;
        LastSeenAt = now.ToUniversalTime();

        return Result.Success();
    }

    public Result Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DEVICE_ALREADY_REVOKED));
        }

        IsRevoked = true;
        RevokedAt = now.ToUniversalTime();

        return Result.Success();
    }

    /// <summary>Token de push sem espaços e com tamanho plausível (FCM/APNs têm dezenas a centenas de caracteres).</summary>
    public static bool IsValidToken(string? token) =>
        !string.IsNullOrEmpty(token)
        && token.Length is >= TokenMinLength and <= TokenMaxLength
        && !token.Any(char.IsWhiteSpace);

    public static bool TryParsePlatform(string? value, out DevicePlatform platform) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out platform) && Enum.IsDefined(platform);

    /// <summary>Sem o token: este objeto pode acabar interpolado num log por engano.</summary>
    public override string ToString() => $"Device {Id} ({Platform})";
}
