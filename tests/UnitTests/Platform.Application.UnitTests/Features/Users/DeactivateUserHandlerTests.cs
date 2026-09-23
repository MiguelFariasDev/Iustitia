using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.DeactivateUser;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class DeactivateUserHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private DeactivateUserHandler CreateHandler() => new(_userRepository, _unitOfWork, _auditService, _currentUser);

    private static User CreateActiveUser(Guid tenantId, UserRole role) =>
        CreateActiveUser(UserId.New(), tenantId, role);

    private static User CreateActiveUser(UserId id, Guid tenantId, UserRole role)
    {
        var user = User.Create(id, tenantId, "Fulano", Email.Create($"{id.Value:N}@teste.com").Value, role).Value;
        user.Activate();
        return user;
    }

    [Fact]
    public async Task Handle_WhenUserIsActiveLawyer_Deactivates()
    {
        var user = CreateActiveUser(Guid.NewGuid(), UserRole.Lawyer);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new DeactivateUserCommand(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyInactive_ReturnsConflict()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new DeactivateUserCommand(user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ALREADY_INACTIVE));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new DeactivateUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenTargetIsCurrentUser_ReturnsCannotDeactivateSelf()
    {
        var user = CreateActiveUser(Guid.NewGuid(), UserRole.Lawyer);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _currentUser.UserId.Returns(user.Id.Value);

        var result = await CreateHandler().Handle(new DeactivateUserCommand(user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_CANNOT_DEACTIVATE_SELF));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTargetIsTheOnlyActiveOwner_ReturnsCannotRemoveLastOwner()
    {
        var tenantId = Guid.NewGuid();
        var owner = CreateActiveUser(tenantId, UserRole.Owner);
        _userRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _userRepository.CountAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(0);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new DeactivateUserCommand(owner.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_CANNOT_REMOVE_LAST_OWNER));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTargetIsOwnerButAnotherActiveOwnerExists_Deactivates()
    {
        var tenantId = Guid.NewGuid();
        var owner = CreateActiveUser(tenantId, UserRole.Owner);
        _userRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _userRepository.CountAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(1);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new DeactivateUserCommand(owner.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        owner.IsActive.Should().BeFalse();
    }
}
