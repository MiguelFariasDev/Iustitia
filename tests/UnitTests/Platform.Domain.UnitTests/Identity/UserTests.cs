using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Identity.Events;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Identity;

public class UserTests
{
    private static Email ValidEmail => Email.Create("advogada@escritorio.com.br").Value;

    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Dra. Ana");
        result.Value.Role.Should().Be(UserRole.Lawyer);
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Create_RaisesUserCreatedEvent()
    {
        var result = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer);

        result.Value.DomainEvents.Should().ContainSingle(e => e is UserCreatedEvent);
    }

    [Fact]
    public void Create_WhenTenantEmpty_Fails()
    {
        var result = User.Create(UserId.New(), Guid.Empty, "Dra. Ana", ValidEmail, UserRole.Lawyer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameMissing_Fails(string? name)
    {
        var result = User.Create(UserId.New(), TenantId, name!, ValidEmail, UserRole.Lawyer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public void Invite_RaisesUserCreatedAndUserInvitedEvents()
    {
        var result = User.Invite(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer);

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().Contain(e => e is UserCreatedEvent);
        result.Value.DomainEvents.Should().Contain(e => e is UserInvitedEvent);
    }

    [Fact]
    public void Activate_WhenInactive_Succeeds()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;
        user.ClearDomainEvents();

        var result = user.Activate();

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(e => e is UserActivatedEvent);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_Fails()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;
        user.Activate();

        var result = user.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ALREADY_ACTIVE));
    }

    [Fact]
    public void Deactivate_WhenActive_Succeeds()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;
        user.Activate();
        user.ClearDomainEvents();

        var result = user.Deactivate();

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle(e => e is UserDeactivatedEvent);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_Fails()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;

        var result = user.Deactivate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ALREADY_INACTIVE));
    }

    [Fact]
    public void UpdateProfile_WhenValid_ChangesFields()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;

        var result = user.UpdateProfile("Dra. Ana Souza", "+55 11 99999-0000", "https://avatar.example/a.png");

        result.IsSuccess.Should().BeTrue();
        user.Name.Should().Be("Dra. Ana Souza");
        user.Phone.Should().Be("+55 11 99999-0000");
        user.AvatarUrl.Should().Be("https://avatar.example/a.png");
    }

    [Fact]
    public void UpdateSpecialty_SetsSpecialty()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;

        user.UpdateSpecialty("Previdenciário");

        user.Specialty.Should().Be("Previdenciário");
    }

    [Fact]
    public void RegisterLogin_SetsLastLoginAt()
    {
        var user = User.Create(UserId.New(), TenantId, "Dra. Ana", ValidEmail, UserRole.Lawyer).Value;
        var now = DateTimeOffset.UtcNow;

        user.RegisterLogin(now);

        user.LastLoginAt.Should().Be(now);
    }
}
