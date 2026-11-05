namespace Advocacia.Workflow.Infrastructure.UnitTests;

/// <summary>
/// Teste que precisa de um PostgreSQL com as migrations aplicadas (variável <c>ADVOCACIA_TEST_DB</c>).
/// Sem a variável ele é PULADO — aparece como "skipped" no resultado, em vez de passar em vazio.
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "ADVOCACIA_TEST_DB";

    public RequiresDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Defina {EnvironmentVariable} com a connection string de um Postgres migrado.";
        }
    }
}
