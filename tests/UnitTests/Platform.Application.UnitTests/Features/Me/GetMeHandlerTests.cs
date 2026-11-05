using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Features.Me.GetMe;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Me;

public sealed class GetMeHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetMeHandlerTests()
    {
        _currentUser.UserId.Returns(_userId);
        _currentUser.TenantId.Returns(_tenantId);
        _currentUser.Email.Returns("ana@escritorio.com.br");
        _currentUser.Roles.Returns(["Lawyer"]);
    }

    private GetMeHandler CreateHandler() => new(_currentUser, _users);

    private User CreateUser(string? oabNumber = "123456", string? oabState = "sp", string? specialty = "Trabalhista")
    {
        var user = User.Create(UserId.From(_userId), _tenantId, "Ana Souza", Email.Create("ana@escritorio.com.br").Value, UserRole.Lawyer).Value;
        if (oabNumber is not null)
        {
            user.UpdateOabNumber(OABNumber.Create(oabNumber, oabState).Value);
        }

        user.UpdateSpecialty(specialty);
        return user;
    }

    [Fact]
    public async Task Handle_ReturnsOabAndSpecialtyFromTheUserProfile()
    {
        _users.GetByIdAsync(UserId.From(_userId), Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await CreateHandler().Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Ana Souza");
        result.Value.OabNumber.Should().Be("123456");
        result.Value.OabState.Should().Be("SP"); // normalizada para maiúsculas pelo value object
        result.Value.Specialty.Should().Be("Trabalhista");
    }

    [Fact]
    public async Task Handle_KeepsTheJwtIdentityFields()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await CreateHandler().Handle(new GetMeQuery(), CancellationToken.None);

        result.Value.UserId.Should().Be(_userId);
        result.Value.TenantId.Should().Be(_tenantId);
        result.Value.Email.Should().Be("ana@escritorio.com.br");
        result.Value.Roles.Should().Equal("Lawyer");
    }

    [Fact]
    public async Task Handle_WhenOabWasNotInformed_ReturnsNullsInsteadOfFailing()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(CreateUser(oabNumber: null, specialty: null));

        var result = await CreateHandler().Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OabNumber.Should().BeNull();
        result.Value.OabState.Should().BeNull();
        result.Value.Specialty.Should().BeNull();
        result.Value.Name.Should().Be("Ana Souza");
    }

    [Fact]
    public async Task Handle_WhenThereIsNoUserRow_FallsBackToTheJwtOnly()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("ana@escritorio.com.br");
        result.Value.Name.Should().BeNull();
        result.Value.OabNumber.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithoutAUserId_DoesNotQueryTheDatabase()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _users.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
