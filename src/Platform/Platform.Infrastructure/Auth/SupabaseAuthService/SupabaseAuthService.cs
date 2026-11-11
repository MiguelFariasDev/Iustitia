using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Auth.SupabaseAuthService;

/// <summary>
/// Integração HTTP com a API do Supabase Auth (GoTrue). O HttpClient injetado já vem
/// configurado (BaseAddress = Supabase URL, header "apikey" = AnonKey, resiliência via
/// Polly) pelo registro em DependencyInjection.AddPlatformAuth — ver também ADR-003.
/// Nunca loga o corpo das requisições/respostas (poderiam conter tokens ou senhas).
/// </summary>
public sealed class SupabaseAuthService(
    HttpClient httpClient,
    IOptions<SupabaseOptions> options,
    ILogger<SupabaseAuthService> logger) : ISupabaseAuthService
{
    private readonly SupabaseOptions _options = options.Value;

    /// <summary>
    /// POST /auth/v1/signup. A resposta tem DUAS formas, decididas pela configuração
    /// "Confirm email" do projeto:
    ///   - confirmação desligada: envelope de sessão — { access_token, ..., user: {...} }
    ///   - confirmação  ligada:   o objeto do usuário direto — { id, email, ... }, SEM token
    /// Tratar só a primeira faz o cadastro de escritório falhar num projeto com confirmação
    /// ligada, DEPOIS de a conta já existir no Supabase — deixando uma conta órfã.
    /// </summary>
    public async Task<Result<SupabaseSignUp>> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/signup")
        {
            Content = JsonContent.Create(new { email, password }),
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase Auth signup falhou com status {StatusCode}", response.StatusCode);
            return Result.Failure<SupabaseSignUp>(await MapErrorAsync(response, cancellationToken));
        }

        var body = await response.Content.ReadFromJsonAsync<SupabaseSignUpPayload>(cancellationToken);
        if (body is null)
        {
            return Result.Failure<SupabaseSignUp>(
                Error.Failure("auth.unexpected_response", "Resposta inesperada do Supabase Auth."));
        }

        // Envelope de sessão: o usuário vem aninhado em "user". Sem envelope, os campos do
        // usuário estão na raiz — Id só é Guid.Empty quando nenhuma das duas formas veio.
        var user = body.User ?? new SupabaseUserPayload(body.Id, body.Email ?? email, body.EmailConfirmedAt);
        if (user.Id == Guid.Empty)
        {
            return Result.Failure<SupabaseSignUp>(
                Error.Failure("auth.unexpected_response", "Resposta inesperada do Supabase Auth."));
        }

        var session = string.IsNullOrEmpty(body.AccessToken)
            ? null
            : new SupabaseSession(user.Id, user.Email, body.AccessToken, body.RefreshToken ?? string.Empty, body.ExpiresIn);

        return Result.Success(new SupabaseSignUp(user.Id, user.Email, session));
    }

    public Task<Result<SupabaseSession>> SignInAsync(string email, string password, CancellationToken cancellationToken = default) =>
        PostForSessionAsync("/auth/v1/token?grant_type=password", new { email, password }, cancellationToken);

    public Task<Result<SupabaseSession>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        PostForSessionAsync("/auth/v1/token?grant_type=refresh_token", new { refresh_token = refreshToken }, cancellationToken);

    public async Task<Result> SignOutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return Result.Success();
        }

        logger.LogWarning("Supabase Auth SignOut falhou com status {StatusCode}", response.StatusCode);
        return Result.Failure(await MapErrorAsync(response, cancellationToken));
    }

    public async Task<Result<Guid>> InviteUserAsync(
        string email, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/invite")
        {
            Content = JsonContent.Create(new { email, data = metadata }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase Auth InviteUser falhou com status {StatusCode}", response.StatusCode);
            return Result.Failure<Guid>(await MapErrorAsync(response, cancellationToken));
        }

        var payload = await response.Content.ReadFromJsonAsync<SupabaseUserPayload>(cancellationToken);
        return payload is null
            ? Result.Failure<Guid>(Error.Failure("auth.unexpected_response", "Resposta inesperada do Supabase Auth."))
            : Result.Success(payload.Id);
    }

    public async Task<Result<SupabaseUserInfo>> GetUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/v1/admin/users/{supabaseUserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        return await GetUserFromRequestAsync(request, cancellationToken);
    }

    public async Task<Result<SupabaseUserInfo>> GetUserFromAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/v1/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await GetUserFromRequestAsync(request, cancellationToken);
    }

    public async Task<Result> SetPasswordAsync(string accessToken, string newPassword, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/auth/v1/user")
        {
            Content = JsonContent.Create(new { password = newPassword }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failure(await MapErrorAsync(response, cancellationToken));
    }

    public async Task<Result> DeleteUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/auth/v1/admin/users/{supabaseUserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            // 404 conta como sucesso: o objetivo é "não existir", e já não existe.
            return Result.Success();
        }

        logger.LogWarning("Supabase Auth DeleteUser falhou com status {StatusCode}", response.StatusCode);
        return Result.Failure(await MapErrorAsync(response, cancellationToken));
    }

    private async Task<Result<SupabaseUserInfo>> GetUserFromRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<SupabaseUserInfo>(await MapErrorAsync(response, cancellationToken));
        }

        var payload = await response.Content.ReadFromJsonAsync<SupabaseUserPayload>(cancellationToken);
        return payload is null
            ? Result.Failure<SupabaseUserInfo>(Error.Unauthorized("auth.invalid_token", "Token inválido ou usuário não encontrado."))
            : Result.Success(new SupabaseUserInfo(payload.Id, payload.Email, payload.EmailConfirmedAt is not null));
    }

    private async Task<Result<SupabaseSession>> PostForSessionAsync(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase Auth {Path} falhou com status {StatusCode}", path, response.StatusCode);
            return Result.Failure<SupabaseSession>(await MapErrorAsync(response, cancellationToken));
        }

        var body = await response.Content.ReadFromJsonAsync<SupabaseSessionPayload>(cancellationToken);
        if (body?.User is null)
        {
            return Result.Failure<SupabaseSession>(Error.Failure("auth.unexpected_response", "Resposta inesperada do Supabase Auth."));
        }

        return new SupabaseSession(body.User.Id, body.User.Email, body.AccessToken, body.RefreshToken, body.ExpiresIn);
    }

    private static async Task<Error> MapErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? message = null;
        string? errorCode = null;

        try
        {
            var errorPayload = await response.Content.ReadFromJsonAsync<SupabaseErrorPayload>(cancellationToken);
            message = errorPayload?.ErrorDescription ?? errorPayload?.Msg ?? errorPayload?.Message;
            errorCode = errorPayload?.ErrorCode;
        }
        catch
        {
            // Resposta sem corpo JSON parseável — segue com mensagem genérica abaixo.
        }

        message ??= "Falha na comunicação com o Supabase Auth.";

        // O Supabase responde 422 para recusas de cadastro que são do usuário (e-mail já em uso,
        // senha fraca, e-mail inválido). Sem este mapeamento viravam 500 genérico e a tela não
        // dizia o que corrigir. O código de erro do GoTrue é estável; a mensagem em inglês, não.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return errorCode switch
            {
                "user_already_exists" or "email_exists" =>
                    Error.Conflict("auth.email_in_use", "Este e-mail já está cadastrado. Entre com ele ou use outro e-mail."),
                "weak_password" =>
                    Error.Validation("auth.weak_password", "Senha fraca demais. Use ao menos 8 caracteres, com letras e números."),
                "email_address_invalid" =>
                    Error.Validation("auth.email_invalid", "O Supabase recusou este e-mail. Use um endereço real."),
                "signup_disabled" =>
                    Error.Failure("auth.signup_disabled", "O cadastro de novas contas está desativado no Supabase."),
                _ => Error.Validation("auth.unprocessable", message),
            };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Error.Unauthorized("auth.invalid_credentials", "E-mail ou senha inválidos."),
            HttpStatusCode.BadRequest => Error.Validation("auth.bad_request", message),
            HttpStatusCode.NotFound => Error.NotFound("auth.not_found", message),
            HttpStatusCode.Conflict => Error.Conflict("auth.conflict", message),
            _ => Error.Failure("auth.supabase_error", message),
        };
    }

    private sealed record SupabaseSessionPayload(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("user")] SupabaseUserPayload? User);

    /// <summary>
    /// Aceita as duas formas da resposta de signup ao mesmo tempo: os campos de sessão
    /// (access_token/user) e os campos do usuário na raiz (id/email). Os ausentes ficam nulos.
    /// </summary>
    private sealed record SupabaseSignUpPayload(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("user")] SupabaseUserPayload? User,
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_confirmed_at")] DateTimeOffset? EmailConfirmedAt);

    private sealed record SupabaseUserPayload(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("email_confirmed_at")] DateTimeOffset? EmailConfirmedAt);

    private sealed record SupabaseErrorPayload(
        [property: JsonPropertyName("error_description")] string? ErrorDescription,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("error_code")] string? ErrorCode);
}
