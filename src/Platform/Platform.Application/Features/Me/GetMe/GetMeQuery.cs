using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Me.GetMe;

/// <summary>Dados do usuário autenticado: identidade do JWT já validado (via ICurrentUser) e perfil (nome, OAB, especialidade) do cadastro.</summary>
public sealed record GetMeQuery : IQuery<GetMeResponse>;
