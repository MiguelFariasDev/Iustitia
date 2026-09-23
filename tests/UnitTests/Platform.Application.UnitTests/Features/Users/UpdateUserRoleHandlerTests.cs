using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.UpdateUserRole;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class UpdateUserRoleHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private UpdateUserRoleHandler CreateHandler() => new(_userRepository, _unitOfWork, _auditService);

    [Fact]
    public async Task Handle_WithDifferentValidRole_ChangesRole()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new UpdateUserRoleCommand(user.Id.Value, "Partner"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(nameof(UserRole.Partner));
        user.Role.Should().Be(UserRole.Partner);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSameRole_ReturnsConflictWithoutSaving()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new UpdateUserRoleCommand(user.Id.Value, "Lawyer"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ReturnsValidationFailureWithoutQueryingRepository()
    {
        var result = await CreateHandler().Handle(new UpdateUserRoleCommand(Guid.NewGuid(), "PapelInexistente"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ROLE_INVALID));
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new UpdateUserRoleCommand(Guid.NewGuid(), "Partner"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }
}
