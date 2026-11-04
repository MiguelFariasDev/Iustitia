using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Me.GetMe;

/// <summary>Dados do usuário autenticado, extraídos do JWT já validado (via ICurrentUser) — sem nenhuma consulta ao banco.</summary>
public sealed record GetMeQuery : IQuery<GetMeResponse>;
