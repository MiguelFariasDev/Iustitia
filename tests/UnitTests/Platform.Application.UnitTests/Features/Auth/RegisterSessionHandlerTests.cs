using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Auth.RegisterSession;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Auth;

/// <summary>
/// O web e o mobile entram direto no Supabase: sem este comando, users.last_login_at ficava NULL e o login
/// nunca era auditado (o LoginHandler só roda em POST /auth/login, que nenhum cliente chama).
/// </summary>
public class RegisterSessionHandlerTests
{
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private RegisterSessionHandler CreateHandler() =>
        new(_userRepository, _unitOfWork, _auditService, _currentUser, _dateTimeProvider);

    private static User CreateUser(Guid userId, Guid tenantId, bool active)
    {
        var user = User.Create(UserId.From(userId), tenantId, "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Owner).Value;
        if (active)
        {
            user.Activate();
        }

        return user;
    }

    [Fact]
    public async Task Handle_ComUsuarioAtivo_GravaUltimoAcessoEAuditaOLogin()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = CreateUser(userId, tenantId, active: true);
        var now = new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
        _currentUser.UserId.Returns(userId);
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(user);
        _dateTimeProvider.UtcNow.Returns(now);

        var result = await CreateHandler().Handle(new RegisterSessionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.LastLoginAt.Should().Be(now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditService.Received(1).RecordAsync(
            AuditAction.Login, nameof(User), userId.ToString(),
            before: null, after: null,
            performedByUserId: userId, tenantId: tenantId, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SemUsuarioNoToken_FalhaSemGravarNada()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new RegisterSessionCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_ComContaDesativada_FalhaSemGravarUltimoAcesso()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, Guid.NewGuid(), active: false);
        _currentUser.UserId.Returns(userId);
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new RegisterSessionCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTH_ACCOUNT_DISABLED));
        user.LastLoginAt.Should().BeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
