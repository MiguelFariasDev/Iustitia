using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using Microsoft.Extensions.Caching.Memory;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Recusa (401 DEVICE_REVOKED) requisições de um dispositivo revogado.
///
/// Como a revogação alcança a API: o app manda <c>X-Device-Id</c> (o id devolvido por
/// <c>POST /devices/register</c>); se aquele dispositivo foi revogado, a resposta é DEVICE_REVOKED
/// e o app encerra a sessão local. Isso <b>não invalida o token do Supabase</b> — o refresh token
/// segue válido no Supabase Auth (ver ADR-069) —; quem o tiver e NÃO enviar o cabeçalho passa
/// por esta checagem. Por isso o app tem de enviá-lo sempre, e a revogação cobre o app oficial e
/// o push, não um atacante que fale com a API diretamente.
///
/// Roda depois do TenantContext (precisa do tenant) e antes da Authorization. Sem o cabeçalho, sem
/// autenticação ou com id desconhecido, não faz nada. A consulta é cacheada por
/// <see cref="CacheTtl"/>: a revogação vale em até esse tempo, em troca de não ir ao banco a cada
/// requisição do app.
/// </summary>
public sealed class RevokedDeviceMiddleware(RequestDelegate next, IMemoryCache cache)
{
    public const string HeaderName = "X-Device-Id";

    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IDeviceRepository devices)
    {
        if (currentUser.IsAuthenticated
            && currentUser.TenantId is { } tenantId
            && Guid.TryParse(context.Request.Headers[HeaderName].ToString(), out var deviceId)
            && deviceId != Guid.Empty)
        {
            var revoked = await cache.GetOrCreateAsync(
                $"device-revoked:{tenantId}:{deviceId}",
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                    return devices.IsRevokedAsync(tenantId, DeviceId.From(deviceId), context.RequestAborted);
                });

            if (revoked)
            {
                await Result.Failure(ErrorFactory.From(ErrorCode.DEVICE_REVOKED)).ToHttpResult().ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }
}
