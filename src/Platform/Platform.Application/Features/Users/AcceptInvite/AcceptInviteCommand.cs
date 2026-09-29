using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.AcceptInvite;

/// <param name="AccessToken">
/// Token retornado pelo SDK do Supabase Auth após o usuário abrir o link de convite
/// (o SDK client-side já valida o token de convite antes de emitir esta sessão
/// provisória) — usado aqui só para identificar o usuário e definir a senha definitiva.
/// </param>
/// <param name="Password">Senha definitiva escolhida pelo usuário.</param>
/// <param name="Name">Nome completo, confirmado/ajustado no aceite do convite.</param>
public sealed record AcceptInviteCommand(string AccessToken, string Password, string Name) : ICommand<AcceptInviteResponse>;
