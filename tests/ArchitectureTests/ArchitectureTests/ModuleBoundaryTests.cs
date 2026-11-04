using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Time;
using Advocacia.Platform.Api.Endpoints;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.Infrastructure.Persistence;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Garante a separação entre Plataforma (BuildingBlocks + Platform) e Módulos de negócio
/// (Advocacia.Modules.*) — ver docs/requisitos/10-arquitetura-e-padroes-referencia.md.
/// BuildingBlocks nunca conhece Platform nem Modules; Platform nunca conhece Modules.
/// Ainda não existem projetos em /src/Modules (chegam a partir da Fase 1), então estes
/// testes hoje passam trivialmente — mas travam a regra assim que os módulos existirem.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly BuildingBlocksDomain = typeof(Entity<>).Assembly;
    private static readonly Assembly BuildingBlocksApplication = typeof(ICurrentUser).Assembly;
    private static readonly Assembly BuildingBlocksInfrastructure = typeof(DateTimeProvider).Assembly;
    private static readonly Assembly PlatformDomain = typeof(Tenant).Assembly;
    private static readonly Assembly PlatformApplication = typeof(CreateTenantCommand).Assembly;
    private static readonly Assembly PlatformInfrastructure = typeof(PlatformDbContext).Assembly;
    private static readonly Assembly PlatformApi = typeof(TenantsEndpoints).Assembly;

    [Theory]
    [MemberData(nameof(BuildingBlocksAssemblies))]
    public void BuildingBlocks_ShouldNotDependOn_Platform(Assembly buildingBlocksAssembly)
    {
        var result = Types.InAssembly(buildingBlocksAssembly)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Platform")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Theory]
    [MemberData(nameof(BuildingBlocksAssemblies))]
    public void BuildingBlocks_ShouldNotDependOn_Modules(Assembly buildingBlocksAssembly)
    {
        var result = Types.InAssembly(buildingBlocksAssembly)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Modules")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Theory]
    [MemberData(nameof(PlatformAssemblies))]
    public void Platform_ShouldNotDependOn_Modules(Assembly platformAssembly)
    {
        var result = Types.InAssembly(platformAssembly)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Modules")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    public static TheoryData<Assembly> BuildingBlocksAssemblies() =>
        new() { BuildingBlocksDomain, BuildingBlocksApplication, BuildingBlocksInfrastructure };

    public static TheoryData<Assembly> PlatformAssemblies() =>
        new() { PlatformDomain, PlatformApplication, PlatformInfrastructure, PlatformApi };

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a separação Plataforma/Módulos: " +
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
