using System.Reflection;
using Advocacia.Platform.Application.Abstractions;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>Convenções de observabilidade/health checks/feature flags (Etapa 0.6 — ver ADR-036/038).</summary>
public class ObservabilityConventionTests
{
    private static readonly Assembly PlatformApi = typeof(Advocacia.Platform.Api.Middleware.CorrelationIdMiddleware).Assembly;
    private static readonly Assembly PlatformInfrastructure = typeof(Advocacia.Platform.Infrastructure.FeatureFlags.FeatureFlagService).Assembly;

    [Fact]
    public void CorrelationIdMiddleware_IsRegisteredInPlatformApi()
    {
        var result = Types.InAssembly(PlatformApi)
            .That().HaveNameEndingWith("Middleware")
            .Should().ResideInNamespace("Advocacia.Platform.Api.Middleware")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void HealthEndpoints_AreDeclaredInApiProgram()
    {
        var programSourcePath = Path.Combine(FindRepositoryRoot(), "src", "Hosts", "Api", "Program.cs");

        File.Exists(programSourcePath).Should().BeTrue($"Program.cs deveria existir em {programSourcePath}");
        var source = File.ReadAllText(programSourcePath);

        source.Should().Contain("/health/live").And.Contain("/health/ready").And.Contain("\"/health\"").And.Contain("/metrics");
    }

    [Fact]
    public void FeatureFlagService_ImplementsIFeatureFlagService()
    {
        var result = Types.InAssembly(PlatformInfrastructure)
            .That().HaveName("FeatureFlagService")
            .Should().ImplementInterface(typeof(IFeatureFlagService))
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
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

        throw new InvalidOperationException(
            $"Não foi possível localizar Advocacia.sln a partir de {AppContext.BaseDirectory}.");
    }
}
