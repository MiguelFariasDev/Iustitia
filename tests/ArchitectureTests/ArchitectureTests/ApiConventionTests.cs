using System.Reflection;
using FluentAssertions;

namespace Advocacia.ArchitectureTests;

/// <summary>Convenções de API da Etapa 0.7 (ver ADR-039/040/041) — versionamento e ordem dos middlewares.</summary>
public class ApiConventionTests
{
    [Fact]
    public void Endpoints_NeverHardcodeApiVersionInRoute_UseMapVersionedGroupInstead()
    {
        var endpointsDirectory = Path.Combine(FindRepositoryRoot(), "src", "Platform", "Platform.Api", "Endpoints");

        foreach (var file in Directory.GetFiles(endpointsDirectory, "*.cs"))
        {
            var source = File.ReadAllText(file);
            source.Should().NotContain(
                "MapGroup(\"/api/v1",
                $"{Path.GetFileName(file)} deveria usar MapVersionedGroup (ver VersionedEndpointExtensions), não uma rota /api/v1 literal.");
        }
    }

    [Fact]
    public void Program_RegistersMiddlewaresInTheDocumentedOrder()
    {
        var programSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Hosts", "Api", "Program.cs"));

        var order = new[]
        {
            "UseMiddleware<CorrelationIdMiddleware>",
            "UseMiddleware<ExceptionHandlingMiddleware>",
            "UseMiddleware<RequestLoggingMiddleware>",
            "UseRateLimiter",
            "UseAuthentication",
            "UseMiddleware<TenantContextMiddleware>",
            "UseAuthorization",
            "UseMiddleware<IdempotencyMiddleware>",
            "MapPlatformEndpoints",
        };

        var lastIndex = -1;
        for (var i = 0; i < order.Length; i++)
        {
            var marker = order[i];
            var index = programSource.IndexOf(marker, StringComparison.Ordinal);
            index.Should().BeGreaterThan(-1, $"Program.cs deveria conter '{marker}'");

            var previousMarker = i > 0 ? order[i - 1] : "(início)";
            index.Should().BeGreaterThan(lastIndex, $"'{marker}' deveria vir depois de '{previousMarker}' — ver ADR-039");
            lastIndex = index;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Advocacia.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Não foi possível localizar Advocacia.sln a partir de {AppContext.BaseDirectory}.");
    }
}
