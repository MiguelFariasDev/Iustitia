using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>Convenções de nomenclatura do claude.md, seção "Padrões de Código e Convenções".</summary>
public class NamingConventionTests
{
    private static readonly Assembly BuildingBlocksDomain = typeof(Entity<>).Assembly;
    private static readonly Assembly BuildingBlocksApplication = typeof(ICurrentUser).Assembly;
    private static readonly Assembly PlatformDomain = typeof(Tenant).Assembly;
    private static readonly Assembly PlatformApplication = typeof(CreateTenantCommand).Assembly;

    [Theory]
    [MemberData(nameof(AllAssemblies))]
    public void Interfaces_ShouldHaveNameStartingWith_I(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .That().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Handlers_ShouldHaveNameEndingWith_Handler()
    {
        var result = Types.InAssembly(PlatformApplication)
            .That().ResideInNamespace("Advocacia.Platform.Application.Features")
            .And().AreClasses()
            .And().DoNotHaveNameEndingWith("Command")
            .And().DoNotHaveNameEndingWith("Query")
            .And().DoNotHaveNameEndingWith("Validator")
            .And().DoNotHaveNameEndingWith("Response")
            .And().DoNotHaveNameEndingWith("Item")
            .And().DoNotHaveNameEndingWith("Specification")
            .Should().HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Commands_ShouldHaveNameEndingWith_Command()
    {
        var result = Types.InAssembly(PlatformApplication)
            .That().ImplementInterface(typeof(ICommand))
            .Or().ImplementInterface(typeof(ICommand<>))
            .Should().HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Queries_ShouldHaveNameEndingWith_Query()
    {
        var result = Types.InAssembly(PlatformApplication)
            .That().ImplementInterface(typeof(IQuery<>))
            .Should().HaveNameEndingWith("Query")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Validators_ShouldHaveNameEndingWith_Validator()
    {
        var result = Types.InAssembly(PlatformApplication)
            .That().Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should().HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    public static TheoryData<Assembly> AllAssemblies() =>
        new() { BuildingBlocksDomain, BuildingBlocksApplication, PlatformDomain, PlatformApplication };

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a convenção de nomenclatura: " +
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
