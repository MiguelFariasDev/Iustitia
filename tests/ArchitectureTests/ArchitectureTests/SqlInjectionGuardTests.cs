using System.Text.RegularExpressions;
using FluentAssertions;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Etapa 3.9, parte 1.4: todo acesso a dados é EF Core/Dapper PARAMETRIZADO. Estes testes varrem o
/// código-fonte e falham se alguém introduzir SQL montado por concatenação ou as variantes "Raw"
/// do EF Core (que aceitam string comum). SQL com <c>$"..."</c> em FromSql/ExecuteSql/SqlQuery é
/// permitido: nessas APIs a interpolação vira parâmetro, não texto.
/// </summary>
public class SqlInjectionGuardTests
{
    private static IEnumerable<string> SourceFiles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Advocacia.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("a raiz do repositório precisa ser encontrada");

        return Directory.EnumerateFiles(Path.Combine(directory!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void NoRawSqlApisThatTakePlainStrings()
    {
        var offenders = SourceFiles()
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\.(FromSqlRaw|ExecuteSqlRaw|ExecuteSqlRawAsync)\s*\("))
            .ToList();

        offenders.Should().BeEmpty("use FromSql/ExecuteSql com string interpolada (parametrizada) ou LINQ");
    }

    [Fact]
    public void NoSqlBuiltByStringConcatenationOrFormat()
    {
        // SELECT/INSERT/UPDATE/DELETE ... seguido de concatenação com variável ou string.Format.
        var concatenation = new Regex(
            @"""\s*(SELECT|INSERT|UPDATE|DELETE)\b[^""]*""\s*\+|string\.Format\(\s*""\s*(SELECT|INSERT|UPDATE|DELETE)\b",
            RegexOptions.IgnoreCase);

        var offenders = SourceFiles().Where(file => concatenation.IsMatch(File.ReadAllText(file))).ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void NoCorsAllowAnyOrigin()
    {
        var offenders = SourceFiles().Where(file => Regex.IsMatch(File.ReadAllText(file), @"\.AllowAnyOrigin\s*\(")).ToList();

        offenders.Should().BeEmpty("CORS estrito é regra inegociável (Etapa 3.9)");
    }
}
