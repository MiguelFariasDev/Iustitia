using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Garante a regra de dependência da Clean Architecture: Domain não conhece Application
/// nem Infrastructure; Application não conhece Infrastructure. Dependências sempre
/// apontam para dentro (ver claude.md, seção 4).
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly BuildingBlocksDomain = typeof(Entity<>).Assembly;
    private static readonly Assembly BuildingBlocksApplication = typeof(ICurrentUser).Assembly;
    private static readonly Assembly PlatformDomain = typeof(Tenant).Assembly;
    private static readonly Assembly PlatformApplication = typeof(CreateTenantCommand).Assembly;

    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_ShouldNotDependOn_Application(Assembly domainAssembly)
    {
        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.BuildingBlocks.Application", "Advocacia.Platform.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_ShouldNotDependOn_Infrastructure(Assembly domainAssembly)
    {
        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.BuildingBlocks.Infrastructure", "Advocacia.Platform.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Theory]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_ShouldNotDependOn_Infrastructure(Assembly applicationAssembly)
    {
        var result = Types.InAssembly(applicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.BuildingBlocks.Infrastructure", "Advocacia.Platform.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    public static TheoryData<Assembly> DomainAssemblies() => [BuildingBlocksDomain, PlatformDomain];

    public static TheoryData<Assembly> ApplicationAssemblies() => [BuildingBlocksApplication, PlatformApplication];

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a regra de dependência: " +
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
