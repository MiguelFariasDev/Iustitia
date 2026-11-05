using Advocacia.BuildingBlocks.Infrastructure.Security;
using Advocacia.Platform.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>
/// Cifra o número da OAB e o telefone dos usuários gravados antes da Etapa 3.9 e re-cifra sob a
/// chave ativa após uma rotação. Idempotente. Mesmas regras de execução (dono do schema, via
/// Worker) de <c>ClienteEncryptionBackfillJob</c> — ver docs/runbooks/field-encryption.md.
/// </summary>
public sealed class UserEncryptionBackfillJob(
    EfDbContext dbContext,
    IOptions<FieldEncryptionOptions> options,
    ILogger<UserEncryptionBackfillJob> logger)
{
    private const int BatchSize = 200;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var activePrefix = $"enc:v1:{options.Value.ActiveKeyId}:";
        var total = 0;

        while (true)
        {
            var ids = await dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM users
                    WHERE (oab_number IS NOT NULL AND oab_number NOT LIKE {activePrefix + "%"})
                       OR (phone IS NOT NULL AND phone NOT LIKE {activePrefix + "%"})
                    ORDER BY id
                    LIMIT {BatchSize}
                    """)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                break;
            }

            var userIds = ids.Select(UserId.From).ToList();
            var users = await dbContext.Set<User>()
                .IgnoreQueryFilters()
                .Where(user => userIds.Contains(user.Id))
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                var entry = dbContext.Entry(user);

                if (user.OabNumber is not null)
                {
                    entry.Property(u => u.OabNumber).IsModified = true;
                }

                if (user.Phone is not null)
                {
                    entry.Property(u => u.Phone).IsModified = true;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            total += users.Count;
            logger.LogInformation("Backfill de criptografia: {Total} usuários processados até agora.", total);
        }

        return total;
    }
}
