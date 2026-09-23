using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Platform.Infrastructure.UnitTests.Jobs;

public sealed class AuditLogCleanupJobTests
{
    private static AuditLog CreateAuditLog(DateTimeOffset createdAt) =>
        AuditLog.Create(Guid.NewGuid(), Guid.NewGuid(), AuditAction.Created, "Tenant", Guid.NewGuid().ToString(), null, null, null, null, createdAt).Value;

    [Fact]
    public async Task RunAsync_RemovesAuditLogsOlderThanRetentionPeriod_KeepsRecentOnes()
    {
        using var dbContext = TestPlatformDbContext.Create();

        var expired = CreateAuditLog(DateTimeOffset.UtcNow.AddYears(-6));
        var withinRetention = CreateAuditLog(DateTimeOffset.UtcNow.AddYears(-4));

        dbContext.Set<AuditLog>().AddRange(expired, withinRetention);
        await dbContext.SaveChangesAsync();

        var sut = new AuditLogCleanupJob(dbContext, NullLogger<AuditLogCleanupJob>.Instance);

        await sut.RunAsync(CancellationToken.None);

        var remaining = await dbContext.Set<AuditLog>().ToListAsync();
        remaining.Should().ContainSingle(log => log.Id == withinRetention.Id);
    }

    [Fact]
    public async Task RunAsync_NeverRemovesAuditLogsWithinLegalRetention()
    {
        using var dbContext = TestPlatformDbContext.Create();

        var almostExpired = CreateAuditLog(DateTimeOffset.UtcNow.AddYears(-5).AddDays(1));
        dbContext.Set<AuditLog>().Add(almostExpired);
        await dbContext.SaveChangesAsync();

        var sut = new AuditLogCleanupJob(dbContext, NullLogger<AuditLogCleanupJob>.Instance);

        await sut.RunAsync(CancellationToken.None);

        (await dbContext.Set<AuditLog>().CountAsync()).Should().Be(1);
    }
}
