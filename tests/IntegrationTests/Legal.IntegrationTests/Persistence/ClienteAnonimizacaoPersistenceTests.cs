using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

[Collection(LegalPostgresCollection.Name)]
public class ClienteAnonimizacaoPersistenceTests(LegalPostgresFixture fixture)
{
    [Fact]
    public async Task Anonymized_clients_keep_the_row_but_leave_no_personal_data_in_the_database()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
        var repository = new ClienteRepository(context);

        // Dois clientes anonimizados NO MESMO escritório: o índice único de documento não pode colidir.
        Cliente Novo(string documento) => Cliente.Create(
            tenantId, TipoCliente.PessoaFisica, NomeCliente.Create("Maria da Silva").Value,
            Documento.Create(documento, TipoCliente.PessoaFisica).Value,
            Contato.Create("maria@exemplo.com.br", "85999998888").Value,
            Endereco.Create("Rua A", "10", null, "Centro", "Fortaleza", "CE", "60000000").Value,
            new DateOnly(1985, 4, 2), "Obs sigilosa").Value;

        var a = Novo("11144477735");
        var b = Novo("52998224725");
        repository.Add(a);
        repository.Add(b);
        await context.SaveChangesAsync();

        a.Anonimizar(DateTimeOffset.UtcNow);
        b.Anonimizar(DateTimeOffset.UtcNow);
        repository.Update(a);
        repository.Update(b);
        await context.SaveChangesAsync();

        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT nome, nome_busca, email, telefone, logradouro, cep, observacoes, documento, anonimizado_em FROM clientes WHERE tenant_id = @t";
        command.Parameters.AddWithValue("t", tenantId);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            reader["nome"].ToString().Should().Be("Titular anonimizado");
            reader["email"].Should().Be(DBNull.Value);
            reader["telefone"].Should().Be(DBNull.Value);
            reader["logradouro"].Should().Be(DBNull.Value);
            reader["cep"].Should().Be(DBNull.Value);
            reader["observacoes"].Should().Be(DBNull.Value);
            reader["anonimizado_em"].Should().NotBe(DBNull.Value);
            // O documento continua cifrado no banco — nunca aparece em claro.
            reader["documento"].ToString().Should().NotContain("11144477735").And.NotContain("52998224725");
        }

        rows.Should().Be(2);
    }
}
