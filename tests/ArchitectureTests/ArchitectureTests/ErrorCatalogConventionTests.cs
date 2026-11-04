using Advocacia.BuildingBlocks.Domain.Errors;
using FluentAssertions;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Convenções do Catálogo de Erros (ADR-032): todo ErrorCode precisa de definição, e
/// nenhum código de erro de negócio pode ser hardcoded fora do catálogo em
/// Platform.Application — ver docs/errors/conventions.md.
/// </summary>
public class ErrorCatalogConventionTests
{
    [Fact]
    public void EveryErrorCode_HasACatalogDefinition()
    {
        var missing = Enum.GetValues<ErrorCode>()
            .Where(code => !ErrorCatalog.TryGet(code, out _))
            .ToList();

        missing.Should().BeEmpty("todo ErrorCode precisa de uma ErrorDefinition em ErrorCatalog");
    }

    [Fact]
    public void ErrorCodes_HaveUniqueValues()
    {
        // Dois membros com o mesmo valor compilam, mas o ErrorCatalog (indexado pelo código)
        // deixaria a última definição sobrescrever a primeira em silêncio. Acontece quando
        // duas branches abrem a mesma faixa numérica e o merge textual não acusa conflito.
        var duplicated = Enum.GetNames<ErrorCode>()
            .GroupBy(name => (int)Enum.Parse<ErrorCode>(name))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group)}")
            .ToList();

        duplicated.Should().BeEmpty("cada ErrorCode precisa de um valor numérico exclusivo");
    }

    [Fact]
    public void PlatformApplicationSourceFiles_NeverConstructErrorDirectly()
    {
        // Result.Failure(Error.Validation/NotFound/Conflict/Unauthorized/Forbidden(...))
        // com string literal é exatamente o padrão pré-Etapa-0.4 que o catálogo substituiu
        // — handlers e Value Objects de Platform.Application/Platform.Domain devem sempre
        // passar por ErrorFactory.From(ErrorCode.X). Infrastructure fica de fora dessa regra
        // de propósito: SupabaseAuthService repassa erros dinâmicos de uma API externa, que
        // não cabem num código fixo do catálogo (ver ResultExtensions.ToProblem).
        var offendingFiles = FindFilesConstructingErrorDirectly(FindSourceDirectory("Platform", "Platform.Application"));

        offendingFiles.Should().BeEmpty(
            "erros de negócio em Platform.Application devem usar ErrorFactory.From(ErrorCode.X), nunca Error.X(\"...\", \"...\") direto");
    }

    [Fact]
    public void PlatformDomainSourceFiles_NeverConstructErrorDirectly()
    {
        var offendingFiles = FindFilesConstructingErrorDirectly(FindSourceDirectory("Platform", "Platform.Domain"));

        offendingFiles.Should().BeEmpty(
            "erros de negócio em Platform.Domain devem usar ErrorFactory.From(ErrorCode.X), nunca Error.X(\"...\", \"...\") direto");
    }

    [Theory]
    [InlineData("Legal.Domain")]
    [InlineData("Legal.Application")]
    public void LegalSourceFiles_NeverConstructErrorDirectly(string projectFolderName)
    {
        var offendingFiles = FindFilesConstructingErrorDirectly(
            FindSourceDirectory("Modules", "Legal", projectFolderName));

        offendingFiles.Should().BeEmpty(
            $"erros de negócio em {projectFolderName} devem usar ErrorFactory.From(ErrorCode.X), nunca Error.X(\"...\", \"...\") direto");
    }

    /// <summary>Localiza src/&lt;segments&gt; a partir do diretório de execução dos testes.</summary>
    private static string FindSourceDirectory(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, "src", .. segments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Não foi possível localizar src/{Path.Combine(segments)} a partir de {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Procura o padrão pré-catálogo (<c>Error.Validation("...", "...")</c> com string literal),
    /// que a ADR-032 substituiu por ErrorFactory.From(ErrorCode.X).
    /// </summary>
    private static List<string> FindFilesConstructingErrorDirectly(string directory) => Directory
        .GetFiles(directory, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .Where(file =>
        {
            var source = File.ReadAllText(file);

            return source.Contains("Error.Validation(")
                || source.Contains("Error.NotFound(")
                || source.Contains("Error.Conflict(")
                || source.Contains("Error.Unauthorized(")
                || source.Contains("Error.Forbidden(")
                || source.Contains("Error.Failure(");
        })
        .ToList();
}
