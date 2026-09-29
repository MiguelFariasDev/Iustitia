using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Domain.Identity;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Users.ListUsers;

public sealed class ListUsersHandler(IRepository<User, UserId> userRepository, IMapper mapper)
    : IQueryHandler<ListUsersQuery, ListUsersResponse>
{
    public async Task<Result<ListUsersResponse>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var parsedRole))
            {
                return Result.Failure<ListUsersResponse>(ErrorFactory.From(ErrorCode.USER_ROLE_INVALID));
            }

            role = parsedRole;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var specification = new ListUsersSpecification(role, request.Specialty, request.IsActive, page, pageSize);

        var items = await userRepository.ListAsync(specification, cancellationToken);

        // CountAsync do Repository ignora paginação/ordenação — só aplica o Criteria,
        // então é seguro reaproveitar a mesma specification para a contagem total.
        var totalCount = await userRepository.CountAsync(specification, cancellationToken);

        var responseItems = items.Select(mapper.Map<UserListItem>).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new ListUsersResponse(responseItems, page, pageSize, totalCount, totalPages);
    }

    private sealed class ListUsersSpecification : Specification<User>
    {
        public ListUsersSpecification(UserRole? role, string? specialty, bool? isActive, int page, int pageSize)
            : base(u =>
                (role == null || u.Role == role) &&
                (specialty == null || u.Specialty == specialty) &&
                (isActive == null || u.IsActive == isActive))
        {
            ApplyOrderBy(u => u.Name);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
