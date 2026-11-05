using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Api.Middleware;
using Advocacia.Platform.Application.Features.Devices;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Advocacia.Platform.Api.Endpoints;

/// <summary>
/// Dispositivos móveis do usuário autenticado: registro (push), lista (tela "Segurança") e
/// revogação. Usuário e escritório vêm do JWT — ninguém vê nem revoga o aparelho de outra pessoa.
/// O token de entrega entra no registro e <b>nunca sai</b> em nenhuma resposta.
/// </summary>
public static class DevicesEndpoints
{
    public static IEndpointRouteBuilder MapDevicesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/devices", "Devices");

        group.MapPost("/register", async (RegisterDeviceCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .WithName("RegisterDevice")
            .WithSummary("Registra (ou renova) o dispositivo do usuário para receber push.")
            .WithDescription(
                "Idempotente por token: o app registra a cada login e a cada renovação do token FCM. " +
                "Devolve o deviceId, que o app deve enviar no cabeçalho X-Device-Id das demais requisições. " +
                "Um token REVOGADO não é reativado: responde 401 DEVICE_REVOKED e o app encerra a sessão local. " +
                "Erros: DEVICE_TOKEN_INVALID, DEVICE_PLATFORM_INVALID (400), DEVICE_REVOKED (401).")
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapGet("/", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListDevicesQuery(ReadCurrentDeviceId(http)), cancellationToken)).ToHttpResult())
            .WithName("ListDevices")
            .WithSummary("Dispositivos do usuário (revogados inclusive), do mais recente ao mais antigo.")
            .WithDescription("Não devolve o token. isCurrent marca o aparelho que fez a requisição (X-Device-Id).")
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new RevokeDeviceCommand(id), cancellationToken)).ToHttpResult())
            .WithName("RevokeDevice")
            .WithSummary("Revoga um dispositivo do usuário (permanente): o push para e a API passa a recusá-lo.")
            .WithDescription(
                "A revogação vale em até 15 s. Não invalida o token do Supabase (ver ADR-069). " +
                "Erros: DEVICE_NOT_FOUND (404, inclusive para o dispositivo de outro usuário), DEVICE_ALREADY_REVOKED (409).")
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        return app;
    }

    private static Guid? ReadCurrentDeviceId(HttpContext http) =>
        Guid.TryParse(http.Request.Headers[RevokedDeviceMiddleware.HeaderName].ToString(), out var id) ? id : null;
}
