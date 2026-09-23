using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.UpdateUser;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class UpdateUserHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private UpdateUserHandler CreateHandler() => new(_userRepository, _unitOfWork, _auditService);

    [Fact]
    public async Task Handle_WhenUserExists_UpdatesProfileAndSpecialty()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Antigo", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new UpdateUserCommand(user.Id.Value, "Novo Nome", "Trabalhista", "https://avatar.png", "11999999999"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Name.Should().Be("Novo Nome");
        user.Specialty.Should().Be("Trabalhista");
        user.Phone.Should().Be("11999999999");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(
            new UpdateUserCommand(Guid.NewGuid(), "Nome", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WithEmptyName_ReturnsValidationFailureWithoutSaving()
    {
        var user = User.Create(UserId.New(), Guid.NewGuid(), "Antigo", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new UpdateUserCommand(user.Id.Value, " ", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
