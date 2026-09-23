using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Auditing;

public class AuditLogTests
{
    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var now = DateTimeOffset.UtcNow;

        var result = AuditLog.Create(
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            action: AuditAction.Login,
            entityType: "User",
            entityId: Guid.NewGuid().ToString(),
            beforeJson: null,
            afterJson: null,
            ip: "127.0.0.1",
            userAgent: "xunit",
            createdAt: now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(AuditAction.Login);
        result.Value.CreatedAt.Should().Be(now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WhenEntityTypeMissing_Fails(string? entityType)
    {
        var result = AuditLog.Create(
            Guid.NewGuid(), Guid.NewGuid(), AuditAction.Created, entityType!, "id", null, null, null, null, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WhenEntityIdMissing_Fails(string? entityId)
    {
        var result = AuditLog.Create(
            Guid.NewGuid(), Guid.NewGuid(), AuditAction.Created, "User", entityId!, null, null, null, null, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }
}
