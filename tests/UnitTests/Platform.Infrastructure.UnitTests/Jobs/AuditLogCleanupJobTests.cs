using System.IO.Compression;
using System.Text;
using Advocacia.Platform.Infrastructure.Auditing.Retention;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.UnitTests.Jobs;

/// <summary>
/// O job só orquestra; as garantias de retenção (5 anos, "só apaga o que está arquivado") são do
/// banco e testadas contra Postgres real em AuditImmutabilityTests (Platform.IntegrationTests).
/// </summary>
public sealed class AuditLogCleanupJobTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateOnly Month = new(2024, 3, 1);

    private static AuditLogArchiveRow Row(int minute) => new(
        Guid.NewGuid(), Tenant, Guid.NewGuid(), "Created", "Cliente", Guid.NewGuid().ToString(),
        null, null, "203.0.113.7", "Mozilla/5.0", new DateTimeOffset(2024, 3, 10, 12, minute, 0, TimeSpan.Zero));

    private static AuditLogCleanupJob CreateJob(FakeMaintenanceStore store, FakeArchiveStore archive) =>
        new(store, archive, Options.Create(new AuditArchiveOptions { BatchLimit = 10 }), NullLogger<AuditLogCleanupJob>.Instance);

    [Fact]
    public async Task Run_ArchivesEachPendingMonth_ThenCommitsWithTheSameHash_ThenPurges()
    {
        var store = new FakeMaintenanceStore { Pending = [new(Tenant, Month, 3)], Rows = [Row(1), Row(2), Row(3)] };
        var archive = new FakeArchiveStore();

        await CreateJob(store, archive).RunAsync(CancellationToken.None);

        archive.Written.Should().ContainSingle();
        archive.Written[0].Name.Should().Be($"{Tenant:D}/2024/2024-03.ndjson.gz");
        store.Commits.Should().ContainSingle();
        store.Commits[0].RowCount.Should().Be(3);
        store.Commits[0].Sha256.Should().Be(archive.Written[0].Sha256, "o banco registra o hash do arquivo que de fato foi gravado");
        store.Purged.Should().BeTrue();
        store.CallOrder.Should().Equal("list", "read", "commit", "purge");
    }

    [Fact]
    public async Task Run_WhenArchiveStoreIsNotConfigured_ArchivesNothing_ButStillRunsTheFiveYearPurge()
    {
        var store = new FakeMaintenanceStore { Pending = [new(Tenant, Month, 1)], Rows = [Row(1)] };

        await CreateJob(store, new FakeArchiveStore { Configured = false }).RunAsync(CancellationToken.None);

        store.Commits.Should().BeEmpty("sem storage frio nada pode sair do banco");
        store.CallOrder.Should().Equal("purge");
    }

    [Fact]
    public async Task Run_WhenWritingToColdStorageFails_NeverCommits_SoNothingIsRemovedFromTheDatabase()
    {
        var store = new FakeMaintenanceStore { Pending = [new(Tenant, Month, 1)], Rows = [Row(1)] };
        var archive = new FakeArchiveStore { FailWith = new IOException("storage indisponível") };

        var act = () => CreateJob(store, archive).RunAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("o Hangfire precisa ver a falha para retentar");
        store.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_OneFailingMonthDoesNotBlockTheOthers()
    {
        var other = Guid.NewGuid();
        var store = new FakeMaintenanceStore
        {
            Pending = [new(Tenant, Month, 1), new(other, Month, 1)],
            Rows = [Row(1)],
            FailReadFor = Tenant,
        };

        var act = () => CreateJob(store, new FakeArchiveStore()).RunAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.Commits.Should().ContainSingle(c => c.TenantId == other);
    }

    [Fact]
    public void Formatter_IsDeterministic_AndProducesOneJsonObjectPerLine()
    {
        var rows = new[] { Row(1), Row(2) };

        var first = AuditArchiveFormatter.Serialize(rows);
        var second = AuditArchiveFormatter.Serialize(rows);

        first.Sha256.Should().Be(second.Sha256);
        first.Content.Should().Equal(second.Content);

        using var gzip = new GZipStream(new MemoryStream(first.Content), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("{").And.Contain("\"entityType\":\"Cliente\"");
    }

    private sealed class FakeMaintenanceStore : IAuditLogMaintenanceStore
    {
        public List<AuditArchiveBatch> Pending { get; init; } = [];

        public List<AuditLogArchiveRow> Rows { get; init; } = [];

        public Guid? FailReadFor { get; init; }

        public List<(Guid TenantId, int RowCount, string Sha256)> Commits { get; } = [];

        public List<string> CallOrder { get; } = [];

        public bool Purged { get; private set; }

        public Task<IReadOnlyList<AuditArchiveBatch>> ListPendingArchiveAsync(int limit, CancellationToken cancellationToken)
        {
            CallOrder.Add("list");
            return Task.FromResult<IReadOnlyList<AuditArchiveBatch>>(Pending);
        }

        public Task<IReadOnlyList<AuditLogArchiveRow>> ReadMonthAsync(Guid tenantId, DateOnly month, CancellationToken cancellationToken)
        {
            CallOrder.Add("read");
            return tenantId == FailReadFor
                ? throw new InvalidOperationException("leitura falhou")
                : Task.FromResult<IReadOnlyList<AuditLogArchiveRow>>(Rows);
        }

        public Task<int> CommitArchiveAsync(
            Guid tenantId, DateOnly month, int rowCount, string sha256, string blobUri, CancellationToken cancellationToken)
        {
            CallOrder.Add("commit");
            Commits.Add((tenantId, rowCount, sha256));
            return Task.FromResult(rowCount);
        }

        public Task<long> PurgeExpiredAsync(CancellationToken cancellationToken)
        {
            CallOrder.Add("purge");
            Purged = true;
            return Task.FromResult(0L);
        }
    }

    private sealed class FakeArchiveStore : IAuditArchiveStore
    {
        public bool Configured { get; init; } = true;

        public Exception? FailWith { get; init; }

        public List<(string Name, string Sha256)> Written { get; } = [];

        public bool IsConfigured => Configured;

        public Task<string> WriteAsync(string blobName, byte[] content, string sha256, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }

            Written.Add((blobName, sha256));
            return Task.FromResult($"https://cold.example/{blobName}");
        }
    }
}
