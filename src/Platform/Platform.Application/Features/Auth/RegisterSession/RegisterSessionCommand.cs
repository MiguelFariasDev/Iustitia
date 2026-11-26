using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Auth.RegisterSession;

/// <summary>
/// Avisa a API de que o usuário autenticado acabou de abrir uma sessão. O web e o mobile entram direto no
/// Supabase Auth (nunca passam por <c>/auth/login</c>), então é isto que grava o último acesso e a auditoria
/// de login. Sem corpo: quem entrou é o dono do token.
/// </summary>
public sealed record RegisterSessionCommand : ICommand;
