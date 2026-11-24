using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Os scripts de rollback (infra/supabase/rollback) só valem se EXECUTAM. Um .down.sql nunca testado costuma
/// falhar exatamente na hora do incidente, quando não há tempo de depurá-lo. Aqui cada um roda, em ordem
/// inversa, contra o banco completo montado pelo fixture — dentro de uma transação que é DESFEITA no fim,
/// então o banco continua intacto para os demais testes da coleção.
///
/// Só a partir da 010: 002 exige confirmação explícita na sessão (desliga o isolamento entre escritórios) e
/// 004 exige desativar o hook no painel do Supabase antes.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RollbackScriptsTests(PostgresFixture fixture)
{
    private static string RollbackDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "infra", "supabase", "rollback");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("infra/supabase/rollback não encontrado a partir do diretório de teste.");
    }

    [Fact]
    public async Task EveryDownScript_FromMigration010On_RunsCleanlyAgainstTheFullSchema()
    {
        var scripts = Directory.GetFiles(RollbackDirectory(), "*.down.sql")
            .Where(file => string.CompareOrdinal(Path.GetFileName(file), "010") >= 0)
            .OrderByDescending(file => Path.GetFileName(file), StringComparer.Ordinal)
            .ToList();
        scripts.Should().NotBeEmpty();

        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        foreach (var script in scripts)
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection, transaction);
            var act = () => command.ExecuteNonQueryAsync();

            await act.Should().NotThrowAsync($"o rollback {Path.GetFileName(script)} precisa executar");
        }

        await transaction.RollbackAsync();
    }

    [Fact]
    public void EveryMigrationFrom010On_HasADownScript_ExceptTheDocumentedOnes()
    {
        var migrations = Path.Combine(RollbackDirectory(), "..", "migrations");
        var semDown = Directory.GetFiles(migrations, "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => string.CompareOrdinal(name, "010") >= 0)
            // 015_account_type_claim substitui a função do hook de token: reverter é reaplicar a versão anterior (ver README).
            .Where(name => name != "015_account_type_claim")
            .Where(name => !File.Exists(Path.Combine(RollbackDirectory(), name + ".down.sql")))
            .ToList();

        semDown.Should().BeEmpty("toda migration nova precisa vir com o seu .down.sql (infra/supabase/rollback/README.md)");
    }
}
