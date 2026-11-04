using System.Reflection;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using Advocacia.Modules.Legal.Application;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Fronteiras da classificação por IA (Etapa 3.2 — ver ADR-067): o módulo Legal fala com a
/// Anthropic e com o catálogo de prompts só pelas PORTAS (Integrations.Anthropic.Application);
/// a integração é genérica e não conhece módulo de negócio.
/// </summary>
public class AiModuleConventionTests
{
    private static readonly Assembly LegalApplication = typeof(LegalApplicationAssembly).Assembly;
    private static readonly Assembly LegalInfrastructure = typeof(LegalDbContext).Assembly;
    private static readonly Assembly AnthropicApplication = typeof(IAnthropicClient).Assembly;
    private static readonly Assembly AnthropicInfrastructure = typeof(AnthropicClient).Assembly;
    private static readonly Assembly WorkflowInfrastructure = typeof(EmbeddedPromptCatalog).Assembly;

    [Fact]
    public void Legal_ShouldNotDependOn_AnthropicOrWorkflowInfrastructure()
    {
        foreach (var assembly in new[] { LegalApplication, LegalInfrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Advocacia.Modules.Integrations.Anthropic.Infrastructure",
                    "Advocacia.Modules.Workflow")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {Failing(result)}");
        }
    }

    [Fact]
    public void LegalApplication_ShouldNotDependOn_Anthropic()
    {
        // As portas de IA do Legal (IClassificationService...) são próprias e não vazam o
        // provedor: trocar de provedor não pode tocar a camada Application.
        var result = Types.InAssembly(LegalApplication)
            .ShouldNot()
            .HaveDependencyOn("Advocacia.Modules.Integrations.Anthropic")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failing(result));
    }

    [Fact]
    public void AnthropicApplication_ShouldNotDependOn_InfrastructureOrBusinessModules()
    {
        var result = Types.InAssembly(AnthropicApplication)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Modules.Integrations.Anthropic.Infrastructure",
                "Advocacia.Modules.Legal",
                "Advocacia.Modules.Workflow")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failing(result));
    }

    [Fact]
    public void AnthropicInfrastructure_ShouldNotDependOn_BusinessModules()
    {
        var result = Types.InAssembly(AnthropicInfrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.Modules.Legal", "Advocacia.Modules.Workflow")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failing(result));
    }

    [Fact]
    public void WorkflowInfrastructure_ShouldNotDependOn_Legal()
    {
        var result = Types.InAssembly(WorkflowInfrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.Modules.Legal", "Advocacia.Modules.Integrations.Anthropic.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failing(result));
    }

    private static string Failing(TestResult result) =>
        "Tipos violando a fronteira: " + string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? []);
}
