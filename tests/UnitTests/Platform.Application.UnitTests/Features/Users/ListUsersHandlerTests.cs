using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Features.Users.ListUsers;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class ListUsersHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();

    private ListUsersHandler CreateHandler() => new(_userRepository, TestMapper.Create());

    private static User CreateUser(string name, UserRole role, bool active)
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), name, Email.Create($"{name.ToLowerInvariant()}@teste.com").Value, role).Value;
        if (active)
        {
            user.Activate();
        }

        return user;
    }

    [Fact]
    public async Task Handle_WithValidFilters_ReturnsMappedPagedItems()
    {
        var users = new List<User> { CreateUser("Ana", UserRole.Lawyer, true) };
        _userRepository.ListAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(users);
        _userRepository.CountAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().Handle(new ListUsersQuery(1, 20, "Lawyer", null, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Name == "Ana" && i.Role == nameof(UserRole.Lawyer));
        result.Value.TotalCount.Should().Be(1);
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ReturnsValidationFailureWithoutQuerying()
    {
        var result = await CreateHandler().Handle(new ListUsersQuery(Role: "PapelInexistente"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ROLE_INVALID));
        await _userRepository.DidNotReceiveWithAnyArgs().ListAsync(default!, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WithOutOfRangePageAndPageSize_NormalizesToDefaults()
    {
        ISpecification<User>? capturedSpecification = null;
        _userRepository.ListAsync(Arg.Do<ISpecification<User>>(s => capturedSpecification = s), Arg.Any<CancellationToken>())
            .Returns([]);
        _userRepository.CountAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler().Handle(new ListUsersQuery(Page: 0, PageSize: 1000), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        capturedSpecification.Should().NotBeNull();
        capturedSpecification!.Skip.Should().Be(0);
        capturedSpecification.Take.Should().Be(20);
    }
}
