using System.Reflection;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using Advocacia.Modules.Legal.Api;
using Advocacia.Modules.Legal.Application;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Convenções do módulo Legal e da integração com o CNJ (Etapa 1.2 — ver ADR-055).
/// Complementa LayerDependencyTests/NamingConventionTests, que hoje cobrem só BuildingBlocks
/// e Platform.
/// </summary>
public class LegalModuleConventionTests
{
    private static readonly Assembly LegalDomain = typeof(Publication).Assembly;
    private static readonly Assembly LegalApplication = typeof(LegalApplicationAssembly).Assembly;
    private static readonly Assembly LegalInfrastructure = typeof(LegalDbContext).Assembly;
    private static readonly Assembly LegalApi = typeof(PublicationsEndpoints).Assembly;
    private static readonly Assembly CnjApplication = typeof(ICnjClient).Assembly;
    private static readonly Assembly CnjInfrastructure = typeof(DjenClient).Assembly;

    [Fact]
    public void LegalDomain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(LegalDomain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Application",
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Modules.Legal.Application",
                "Advocacia.Modules.Legal.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void LegalApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(LegalApplication)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Platform.Infrastructure",
                "Advocacia.Modules.Legal.Infrastructure",
                "Advocacia.Modules.Integrations.CNJ.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void LegalModule_ShouldNotDependOn_Platform()
    {
        // O módulo de negócio consome a PLATAFORMA através de BuildingBlocks, não de Platform.*
        // (que é o núcleo de identidade/tenancy, com seus próprios agregados) — ver ADR-001.
        foreach (var assembly in new[] { LegalDomain, LegalApplication, LegalInfrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Advocacia.Platform")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {BuildFailureMessage(result)}");
        }
    }

    [Fact]
    public void LegalModule_ShouldNotDependOn_AnotherBusinessModulesInfrastructure()
    {
        // Legal.Infrastructure depende da PORTA do CNJ (Integrations.CNJ.Application), nunca da
        // implementação: quem registra o DjenClient é o host (ver Hosts/Api/Program.cs). Sem essa
        // regra, o acoplamento entre módulos volta pela porta dos fundos da camada de infra.
        var result = Types.InAssembly(LegalInfrastructure)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Modules.Integrations.CNJ.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void LegalApi_MayUseThePlatformPlumbing_ButNotItsDomainModel()
    {
        // Legal.Api depende de Platform.Api/Platform.Infrastructure de propósito: é de lá que
        // vêm MapVersionedGroup, ResultExtensions e as Policies de autorização — encanamento
        // web compartilhado, não regra de negócio.
        //
        // O que continua proibido é o módulo jurídico conhecer os AGREGADOS da plataforma:
        // publicação não sabe o que é Tenant nem User. Essa é a linha que separa "usar a
        // plataforma" de "acoplar ao núcleo".
        var result = Types.InAssembly(LegalApi)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.Platform.Domain", "Advocacia.Platform.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void LegalApi_ShouldNotDependOn_AnotherBusinessModule()
    {
        var result = Types.InAssembly(LegalApi)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Modules.Integrations")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void CnjApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CnjApplication)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Modules.Integrations.CNJ.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void CnjIntegration_ShouldNotDependOn_BusinessModules()
    {
        // A integração é genérica: quem consome publicações do CNJ é o módulo Legal, e a
        // dependência aponta só nessa direção.
        foreach (var assembly in new[] { CnjApplication, CnjInfrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Advocacia.Modules.Legal")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {BuildFailureMessage(result)}");
        }
    }

    [Fact]
    public void Handlers_ShouldHaveNameEndingWith_Handler()
    {
        var result = Types.InAssembly(LegalApplication)
            .That().ResideInNamespace("Advocacia.Modules.Legal.Application.Features")
            .And().AreClasses()
            .And().DoNotHaveNameEndingWith("Command")
            .And().DoNotHaveNameEndingWith("Query")
            .And().DoNotHaveNameEndingWith("Validator")
            .And().DoNotHaveNameEndingWith("Response")
            .And().DoNotHaveNameEndingWith("Item")
            .And().DoNotHaveNameEndingWith("Specification")
            // Request: DTO de entrada que não é o Command em si (ex.: ContatoRequest, aninhado).
            // Mapping: tradutor estático Domain -> contrato de resposta.
            .And().DoNotHaveNameEndingWith("Request")
            .And().DoNotHaveNameEndingWith("Mapping")
            .Should().HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Commands_ShouldHaveNameEndingWith_Command()
    {
        var result = Types.InAssembly(LegalApplication)
            .That().ImplementInterface(typeof(ICommand))
            .Or().ImplementInterface(typeof(ICommand<>))
            .Should().HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Queries_ShouldHaveNameEndingWith_Query()
    {
        var result = Types.InAssembly(LegalApplication)
            .That().ImplementInterface(typeof(IQuery<>))
            .Should().HaveNameEndingWith("Query")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Validators_ShouldHaveNameEndingWith_Validator()
    {
        var result = Types.InAssembly(LegalApplication)
            .That().Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should().HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Specifications_ShouldHaveNameEndingWith_Specification()
    {
        var result = Types.InAssembly(LegalDomain)
            .That().ResideInNamespace("Advocacia.Modules.Legal.Domain.Specifications")
            .And().AreClasses()
            .Should().HaveNameEndingWith("Specification")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Theory]
    [MemberData(nameof(LegalAssemblies))]
    public void Interfaces_ShouldHaveNameStartingWith_I(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .That().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    public static TheoryData<Assembly> LegalAssemblies() =>
        new() { LegalDomain, LegalApplication, LegalInfrastructure, CnjApplication, CnjInfrastructure };

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a convenção: " + string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? []);
}
