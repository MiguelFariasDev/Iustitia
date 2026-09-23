using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.GetUserById;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class GetUserByIdHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();

    private GetUserByIdHandler CreateHandler() => new(_userRepository, TestMapper.Create());

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsUserData()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new GetUserByIdQuery(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id.Value);
        result.Value.Email.Should().Be("fulano@teste.com");
        result.Value.Role.Should().Be(nameof(UserRole.Lawyer));
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }
}
