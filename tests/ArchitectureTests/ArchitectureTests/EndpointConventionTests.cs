using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Endpoints (Minimal API) só podem orquestrar MediatR — nunca conter lógica de negócio
/// (repositórios, UnitOfWork, entidades de domínio direto). Ver Part 7.2 do prompt da
/// Etapa 0.4 e EndpointRouteBuilderExtensions.
/// </summary>
public class EndpointConventionTests
{
    private static readonly Assembly PlatformApi = typeof(Platform.Api.Extensions.ResultExtensions).Assembly;

    [Fact]
    public void Endpoints_ShouldNotDependOnRepositoriesOrUnitOfWork()
    {
        var result = Types.InAssembly(PlatformApi)
            .That().ResideInNamespace("Advocacia.Platform.Api.Endpoints")
            .Should().NotHaveDependencyOnAny(
                typeof(IRepository<,>).Namespace!,
                typeof(IUnitOfWork).FullName!)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Endpoints devem só chamar ISender.Send — acesso a dados é responsabilidade exclusiva dos Handlers");
    }

    [Fact]
    public void Endpoints_ShouldNotDependDirectlyOnDomainEntities()
    {
        var result = Types.InAssembly(PlatformApi)
            .That().ResideInNamespace("Advocacia.Platform.Api.Endpoints")
            .Should().NotHaveDependencyOn(typeof(Tenant).Namespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Endpoints devem depender só de Commands/Queries/Responses de Platform.Application, nunca de entidades de domínio");
    }
}
