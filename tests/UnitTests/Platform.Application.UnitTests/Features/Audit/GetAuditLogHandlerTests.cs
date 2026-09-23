using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Features.Audit.GetAuditLog;
using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Audit;

public class GetAuditLogHandlerTests
{
    private readonly IReadRepository<AuditLog, AuditLogId> _auditLogRepository = Substitute.For<IReadRepository<AuditLog, AuditLogId>>();

    private GetAuditLogHandler CreateHandler() => new(_auditLogRepository, TestMapper.Create());

    [Fact]
    public async Task Handle_WhenAuditLogExists_ReturnsData()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var auditLog = AuditLog.Create(tenantId, userId, AuditAction.Login, "User", userId.ToString(), null, null, null, null, now).Value;

        _auditLogRepository.GetByIdAsync(auditLog.Id, Arg.Any<CancellationToken>()).Returns(auditLog);

        var result = await CreateHandler().Handle(new GetAuditLogQuery(auditLog.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Action.Should().Be(nameof(AuditAction.Login));
        result.Value.CreatedAt.Should().Be(now);
    }

    [Fact]
    public async Task Handle_WhenAuditLogDoesNotExist_ReturnsNotFound()
    {
        _auditLogRepository.GetByIdAsync(Arg.Any<AuditLogId>(), Arg.Any<CancellationToken>()).Returns((AuditLog?)null);

        var result = await CreateHandler().Handle(new GetAuditLogQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUDIT_LOG_NOT_FOUND));
    }
}
