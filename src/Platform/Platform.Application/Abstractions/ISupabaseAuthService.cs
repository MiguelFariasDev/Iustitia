using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Abstração sobre a API do Supabase Auth. A implementação real (HTTP + Polly) vive em
/// Platform.Infrastructure — esta interface fica em Application para que os handlers
/// dependam de uma abstração, nunca de infraestrutura concreta (regra de dependência da
/// Clean Architecture, ver ADR-005 e ArchitectureTests.LayerDependencyTests).
/// </summary>
public interface ISupabaseAuthService
{
    /// <summary>
    /// Cadastra o usuário no Supabase Auth. NÃO devolve sessão: quando a confirmação de
    /// e-mail está ligada no projeto (Authentication &gt; Providers &gt; Email &gt; Confirm
    /// email), o Supabase cria o usuário e responde SEM access_token — só depois de o
    /// e-mail ser confirmado existe sessão. Por isso o retorno carrega o Id sempre e a
    /// sessão só quando ela de fato veio (ver <see cref="SupabaseSignUp"/>).
    /// </summary>
    Task<Result<SupabaseSignUp>> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result<SupabaseSession>> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result<SupabaseSession>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<Result> SignOutAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Envia e-mail de convite; o usuário é criado no Supabase Auth em estado "convidado".</summary>
    Task<Result<Guid>> InviteUserAsync(
        string email, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);

    Task<Result<SupabaseUserInfo>> GetUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default);

    /// <summary>"Quem sou eu": resolve o usuário a partir do próprio access token (GET /auth/v1/user).</summary>
    Task<Result<SupabaseUserInfo>> GetUserFromAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo fluxo de aceite de convite: define a senha definitiva do usuário convidado.</summary>
    Task<Result> SetPasswordAsync(string accessToken, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove o usuário do Supabase Auth. Existe para COMPENSAÇÃO: o cadastro de escritório
    /// cria a conta no Supabase antes de gravar o Tenant local, e sem isso uma falha no meio
    /// deixaria uma conta órfã — que bloquearia a nova tentativa com "e-mail já cadastrado".
    /// </summary>
    Task<Result> DeleteUserAsync(Guid supabaseUserId, CancellationToken cancellationToken = default);
}

/// <summary>Sessão retornada pelo Supabase Auth após login/refresh/signup bem-sucedido.</summary>
public sealed record SupabaseSession(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);

/// <summary>
/// Resultado do cadastro no Supabase Auth. <paramref name="Session"/> é nula quando o
/// projeto exige confirmação de e-mail — o usuário existe, mas ainda não pode entrar.
/// </summary>
/// <param name="UserId">Id do usuário no Supabase, usado como Id do User local.</param>
/// <param name="Email">E-mail cadastrado, como o Supabase o normalizou.</param>
/// <param name="Session">Sessão já emitida, ou nula se falta confirmar o e-mail.</param>
public sealed record SupabaseSignUp(Guid UserId, string Email, SupabaseSession? Session)
{
    /// <summary>Verdadeiro quando o usuário precisa confirmar o e-mail antes do primeiro login.</summary>
    public bool RequiresEmailConfirmation => Session is null;
}

/// <summary>Dados de um usuário do Supabase Auth (não confundir com o agregado User do domínio).</summary>
public sealed record SupabaseUserInfo(Guid Id, string Email, bool EmailConfirmed);
