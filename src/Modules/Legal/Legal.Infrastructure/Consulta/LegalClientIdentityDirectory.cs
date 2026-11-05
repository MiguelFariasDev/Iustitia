using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Consulta;

/// <summary>
/// <see cref="IClientIdentityDirectory"/> sobre o LegalDbContext. Toda consulta filtra o tenant
/// explicitamente, além do filtro global do EF e do RLS: aqui um erro entregaria dado de um escritório
/// a outro, então cada camada de defesa se paga.
/// </summary>
public sealed class LegalClientIdentityDirectory(LegalDbContext dbContext) : IClientIdentityDirectory
{
    public async Task<Guid?> FindActiveClientByPhoneAsync(
        Guid tenantId, string whatsAppPhone, CancellationToken cancellationToken = default)
    {
        var candidates = BrazilianPhone.NationalCandidates(whatsAppPhone);
        if (candidates.Count == 0)
        {
            return null;
        }

        // Take(2): basta saber se há UM ou MAIS DE UM. Dois clientes com o mesmo telefone (a mãe e o
        // filho, o sócio e a empresa) não identificam ninguém.
        var matches = await dbContext.Clientes
            .Where(cliente => cliente.TenantId == tenantId
                && cliente.IsActive
                && cliente.Contato.Telefone != null
                && candidates.Contains(cliente.Contato.Telefone))
            .Select(cliente => cliente.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return matches.Count == 1 ? matches[0].Value : null;
    }

    public async Task<bool> DocumentMatchesAsync(
        Guid tenantId, Guid clientId, string documentDigits, CancellationToken cancellationToken = default)
    {
        var id = ClienteId.From(clientId);

        // Lê só a coluna derivada com os dígitos, e só para comparar: não volta para ninguém.
        var stored = await dbContext.Clientes
            .Where(cliente => cliente.TenantId == tenantId && cliente.Id == id && cliente.IsActive)
            .Select(cliente => cliente.DocumentoBusca)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(documentDigits))
        {
            return false;
        }

        // Tempo constante sobre os HASHES: iguala os tamanhos (CPF x CNPJ) e não revela, pelo tempo,
        // quantos dígitos iniciais acertaram.
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(stored)),
            SHA256.HashData(Encoding.UTF8.GetBytes(documentDigits)));
    }
}
