using Advocacia.BuildingBlocks.Infrastructure.Security;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Cifra os dados de cliente gravados ANTES da Etapa 3.9 (texto puro) e recalcula o índice cego.
/// Também serve à ROTAÇÃO de chave: re-cifra tudo que não esteja sob a chave ativa. Idempotente —
/// pode rodar de novo a qualquer momento e só toca linhas que ainda precisam.
///
/// Precisa rodar com a connection string do DONO do schema (como as migrations), não com
/// app_user: o RLS esconde as linhas de todos os tenants de quem não definiu um. Executado
/// pelo Worker com <c>--backfill-field-encryption</c> (ver docs/runbooks/field-encryption.md).
///
/// Ordem do deploy: migration (colunas mais largas, índice único no índice cego) → backfill →
/// só então <c>Encryption:AllowLegacyPlaintext=false</c>. Entre a migration e o backfill, o
/// cadastro de um documento já existente em texto puro não é detectado como duplicado; rode o
/// backfill antes de reabrir o tráfego (blue/green, ver scripts/deploy).
/// </summary>
public sealed class ClienteEncryptionBackfillJob(
    LegalDbContext dbContext,
    IOptions<FieldEncryptionOptions> options,
    ILogger<ClienteEncryptionBackfillJob> logger)
{
    private const int BatchSize = 200;
    private const int BlindIndexLength = 64;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var activePrefix = $"enc:v1:{options.Value.ActiveKeyId}:";
        var total = 0;

        while (true)
        {
            // SQL fixo (sem concatenação de entrada): só o prefixo da chave ativa, que vem de
            // configuração, entra como parâmetro.
            var ids = await dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM clientes
                    WHERE documento NOT LIKE {activePrefix + "%"}
                       OR length(documento_busca) <> {BlindIndexLength}
                       OR (email IS NOT NULL AND email NOT LIKE {activePrefix + "%"})
                       OR (telefone IS NOT NULL AND telefone NOT LIKE {activePrefix + "%"})
                    ORDER BY id
                    LIMIT {BatchSize}
                    """)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                break;
            }

            // Id é value object convertido: a lista precisa ser de ClienteId (não de Guid) para o
            // EF traduzir o Contains.
            var clienteIds = ids.Select(ClienteId.From).ToList();
            var clientes = await dbContext.Set<Cliente>()
                .IgnoreQueryFilters()
                .Where(cliente => clienteIds.Contains(cliente.Id))
                .ToListAsync(cancellationToken);

            foreach (var cliente in clientes)
            {
                var entry = dbContext.Entry(cliente);

                // Re-gravar o valor lido (já decifrado/legado) faz o conversor cifrá-lo com a chave ativa.
                entry.Property(c => c.Documento).IsModified = true;

                var contato = entry.Reference(c => c.Contato).TargetEntry;
                if (contato is not null)
                {
                    if (cliente.Contato.Email is not null)
                    {
                        contato.Property(c => c.Email).IsModified = true;
                    }

                    if (cliente.Contato.Telefone is not null)
                    {
                        contato.Property(c => c.Telefone).IsModified = true;
                    }
                }

                // O índice cego só é recalculado quando a coluna ainda tem o dígito em claro:
                // um valor já com 64 caracteres é o HMAC, e re-gravá-lo faria HMAC do HMAC.
                if (cliente.DocumentoBusca.Length != BlindIndexLength)
                {
                    entry.Property(c => c.DocumentoBusca).IsModified = true;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            total += clientes.Count;
            logger.LogInformation("Backfill de criptografia: {Total} clientes processados até agora.", total);
        }

        return total;
    }
}
