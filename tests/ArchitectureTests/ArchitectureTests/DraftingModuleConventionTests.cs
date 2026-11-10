using System.Reflection;
using Advocacia.Modules.Drafting.Application;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using FluentAssertions;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Convenções do módulo Drafting (ADR-072). As duas últimas são travas de segurança, não de estilo:
/// o cliente do Qdrant só pode ser usado onde o filtro por tenant é aplicado, e a URL do modelo
/// só pode ser conhecida pela camada de infraestrutura.
/// </summary>
public class DraftingModuleConventionTests
{
    private static readonly Assembly Domain = typeof(LegalDocumentRequest).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(DraftingDbContext).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny("Advocacia.Modules.Drafting.Application", "Advocacia.Modules.Drafting.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure_NorEntityFrameworkNorHttp()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny(
                "Advocacia.Modules.Drafting.Infrastructure", "Microsoft.EntityFrameworkCore", "Qdrant.Client")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a Application fala só com as portas (ILlmService, IEmbeddingService, IVectorStore); trocar de provedor não pode tocá-la");
    }

    [Fact]
    public void QdrantClient_IsOnlyUsedInsideTheQdrantNamespace_WhereClientDataStoreAlwaysAppliesTheTenantFilter()
    {
        var result = Types.InAssembly(Infrastructure)
            .That().DoNotResideInNamespace("Advocacia.Modules.Drafting.Infrastructure.Qdrant")
            .ShouldNot().HaveDependencyOn("Qdrant.Client")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"o Qdrant não tem RLS: só QdrantService (que sempre filtra por tenant) e NormaQdrantStore (acervo público, sem dado de cliente) podem falar com o cliente. Violam: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
    }

    [Fact]
    public void OllamaTypes_StayInTheirInfrastructureNamespace()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Advocacia.Modules.Drafting.Infrastructure.Ollama")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
