using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Features.Audit.ListAuditLogs;
using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Audit;

public class ListAuditLogsHandlerTests
{
    private readonly IReadRepository<AuditLog, AuditLogId> _auditLogRepository = Substitute.For<IReadRepository<AuditLog, AuditLogId>>();

    private ListAuditLogsHandler CreateHandler() => new(_auditLogRepository, TestMapper.Create());

    [Fact]
    public async Task Handle_ReturnsMappedPagedItems()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auditLog = AuditLog.Create(tenantId, userId, AuditAction.Login, "User", userId.ToString(), null, null, null, null, DateTimeOffset.UtcNow).Value;

        _auditLogRepository.ListAsync(Arg.Any<ISpecification<AuditLog>>(), Arg.Any<CancellationToken>()).Returns([auditLog]);
        _auditLogRepository.CountAsync(Arg.Any<ISpecification<AuditLog>>(), Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().Handle(new ListAuditLogsQuery(1, 20, "User", userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.UserId == userId && i.Action == nameof(AuditAction.Login));
        result.Value.TotalCount.Should().Be(1);
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithOutOfRangePageSize_NormalizesToDefault()
    {
        ISpecification<AuditLog>? capturedSpecification = null;
        _auditLogRepository.ListAsync(Arg.Do<ISpecification<AuditLog>>(s => capturedSpecification = s), Arg.Any<CancellationToken>())
            .Returns([]);
        _auditLogRepository.CountAsync(Arg.Any<ISpecification<AuditLog>>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler().Handle(new ListAuditLogsQuery(PageSize: 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(20);
        capturedSpecification.Should().NotBeNull();
        capturedSpecification!.Take.Should().Be(20);
    }
}
