using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Me.GetMe;

/// <summary>
/// Identidade vem do JWT (já validado); nome, OAB e especialidade vêm do CADASTRO do usuário,
/// que o token não carrega. Uma leitura por chamada, filtrada por tenant (filtro global + RLS).
///
/// Se o cadastro não existir (ex.: usuário recém-criado no Supabase Auth ainda sem linha em
/// <c>users</c>), responde só com o que o JWT diz — o app continua entrando, sem OAB.
/// </summary>
public sealed class GetMeHandler(ICurrentUser currentUser, IRepository<User, UserId> users)
    : IQueryHandler<GetMeQuery, GetMeResponse>
{
    public async Task<Result<GetMeResponse>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        User? profile = null;

        if (currentUser.UserId is { } userId && userId != Guid.Empty)
        {
            profile = await users.GetByIdAsync(UserId.From(userId), cancellationToken);
        }

        return Result.Success(new GetMeResponse(
            currentUser.UserId,
            currentUser.Email,
            currentUser.TenantId,
            currentUser.Roles,
            profile?.Name,
            profile?.OabNumber?.Number,
            profile?.OabNumber?.StateCode,
            profile?.Specialty));
    }
}
