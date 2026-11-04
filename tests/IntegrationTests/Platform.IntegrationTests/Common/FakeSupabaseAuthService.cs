using System.Collections.Concurrent;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Substitui <see cref="ISupabaseAuthService"/> nos testes de integração de ponta a ponta
/// (não existe um projeto Supabase real neste ambiente — ver docs/compliance/auth-flow.md).
/// Simula o suficiente do comportamento real do Supabase Auth para exercitar os handlers
/// de Auth/Tenants/Users por trás dos endpoints HTTP de verdade: cadastro, login, convite e
/// aceite de convite, com estado em memória (um dicionário por e-mail).
///
/// O "access token" emitido é apenas o Id do usuário como string — os testes nunca chamam
/// um JWKS de verdade para validar chamadas a este fake (só o JwtBearer do host, que usa a
/// chave RSA de teste, ver TestJwtTokenFactory).
/// </summary>
public sealed class FakeSupabaseAuthService : ISupabaseAuthService
{
    private sealed record FakeUser(Guid Id, string Email, string? Password);

    private readonly ConcurrentDictionary<string, FakeUser> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, string> _emailsById = new();

    /// <summary>
    /// Espelha a opção "Confirm email" do projeto Supabase. Quando ligada, o cadastro cria o
    /// usuário mas NÃO devolve sessão — foi o caso que quebrou o cadastro de escritório em
    /// produção (ver CreateTenantHandler).
    /// </summary>
    public bool RequiresEmailConfirmation { get; set; }

    public Task<Result<SupabaseSignUp>> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (_usersByEmail.ContainsKey(email))
        {
            return Task.FromResult(Result.Failure<SupabaseSignUp>(
                Error.Conflict("auth.email_already_registered", "E-mail já cadastrado.")));
        }

        var user = new FakeUser(Guid.NewGuid(), email, password);
        _usersByEmail[email] = user;
        _emailsById[user.Id] = email;

        return Task.FromResult(Result.Success(new SupabaseSignUp(
            user.Id, user.Email, RequiresEmailConfirmation ? null : BuildSession(user))));
    }

    public Task<Result<SupabaseSession>> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (!_usersByEmail.TryGetValue(email, out var user) || user.Password != password)
        {
            return Task.FromResult(Result.Failure<SupabaseSession>(
                Error.Unauthorized("auth.invalid_credentials", "Credenciais inválidas.")));
        }

        return Task.FromResult(Result.Success(BuildSession(user)));
    }

    public Task<Result<SupabaseSession>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(refreshToken.Replace("refresh-", string.Empty), out var userId) ||
            !_emailsById.TryGetValue(userId, out var email) ||
            !_usersByEmail.TryGetValue(email, out var user))
        {
            return Task.FromResult(Result.Failure<SupabaseSession>(
                Error.Unauthorized("auth.invalid_refresh_token", "Refresh token inválido ou expirado.")));
        }

        return Task.FromResult(Result.Success(BuildSession(user)));
    }

    public Task<Result> SignOutAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public Task<Result<Guid>> InviteUserAsync(
        string email, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        if (_usersByEmail.ContainsKey(email))
        {
            return Task.FromResult(Result.Failure<Guid>(
                Error.Conflict("auth.email_already_registered", "E-mail já cadastrado.")));
        }

        var user = new FakeUser(Guid.NewGuid(), email, Password: null);
        _usersByEmail[email] = user;
        _emailsById[user.Id] = email;

        return Task.FromResult(Result.Success(user.Id));
    }

    public Task<Result<SupabaseUserInfo>> GetUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default) =>
        ResolveByAccessToken(supabaseUserId.ToString());

    public Task<Result<SupabaseUserInfo>> GetUserFromAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default) =>
        ResolveByAccessToken(accessToken);

    public Task<Result> SetPasswordAsync(string accessToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(accessToken, out var userId) ||
            !_emailsById.TryGetValue(userId, out var email) ||
            !_usersByEmail.TryGetValue(email, out var user))
        {
            return Task.FromResult(Result.Failure(Error.Unauthorized("auth.invalid_token", "Token inválido.")));
        }

        _usersByEmail[email] = user with { Password = newPassword };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default)
    {
        if (_emailsById.TryRemove(supabaseUserId, out var email))
        {
            _usersByEmail.TryRemove(email, out _);
        }

        // Apagar quem não existe é sucesso: o objetivo é "não existir".
        return Task.FromResult(Result.Success());
    }

    /// <summary>Id Supabase que o cadastro/convite mais recente para este e-mail recebeu — para o teste montar um JWT.</summary>
    public Guid GetUserIdByEmail(string email) => _usersByEmail[email].Id;

    /// <summary>Para o teste conferir que a compensação apagou (ou não) a conta.</summary>
    public bool HasUser(string email) => _usersByEmail.ContainsKey(email);

    private Task<Result<SupabaseUserInfo>> ResolveByAccessToken(string accessToken)
    {
        if (!Guid.TryParse(accessToken, out var userId) || !_emailsById.TryGetValue(userId, out var email))
        {
            return Task.FromResult(Result.Failure<SupabaseUserInfo>(
                Error.Unauthorized("auth.invalid_token", "Token inválido.")));
        }

        return Task.FromResult(Result.Success(new SupabaseUserInfo(userId, email, EmailConfirmed: true)));
    }

    // access_token = "{id}" e refresh_token = "refresh-{id}": suficiente para os testes
    // resolverem o usuário de volta sem precisar de um JWT de verdade (o JWT só é exigido
    // pelo JwtBearer do host — ver TestJwtTokenFactory — nunca por este fake).
    private static SupabaseSession BuildSession(FakeUser user) =>
        new(user.Id, user.Email, user.Id.ToString(), $"refresh-{user.Id}", ExpiresInSeconds: 3600);
}
