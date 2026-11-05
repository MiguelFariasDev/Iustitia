using System.Reflection;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.Modules.Atendimentos.Api;
using Advocacia.Modules.Atendimentos.Application;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Events;
using Advocacia.Modules.Atendimentos.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// Convenções do módulo Atendimento (inbox + handoff do bot de WhatsApp — ver ADR-067).
/// Mesmas regras de LegalModuleConventionTests: módulo de negócio consome a plataforma por
/// BuildingBlocks, nunca por Platform.*, e nunca conhece outro módulo.
/// </summary>
public class AtendimentoModuleConventionTests
{
    private static readonly Assembly AtendimentoDomain = typeof(Atendimento).Assembly;
    private static readonly Assembly AtendimentoApplication = typeof(AtendimentoApplicationAssembly).Assembly;
    private static readonly Assembly AtendimentoInfrastructure = typeof(AtendimentoDbContext).Assembly;
    private static readonly Assembly AtendimentoApi = typeof(BotEndpoints).Assembly;

    [Fact]
    public void AtendimentoDomain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(AtendimentoDomain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Application",
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Modules.Atendimentos.Application",
                "Advocacia.Modules.Atendimentos.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void AtendimentoApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(AtendimentoApplication)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Advocacia.BuildingBlocks.Infrastructure",
                "Advocacia.Platform.Infrastructure",
                "Advocacia.Modules.Atendimentos.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void AtendimentoModule_ShouldNotDependOn_Platform()
    {
        // Por isso IRealtimeNotifier vive em BuildingBlocks.Application (ADR-067): o módulo
        // publica em tempo real sem conhecer o Platform.
        foreach (var assembly in new[] { AtendimentoDomain, AtendimentoApplication, AtendimentoInfrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Advocacia.Platform")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {BuildFailureMessage(result)}");
        }
    }

    [Fact]
    public void AtendimentoApi_MayUseThePlatformPlumbing_ButNotItsDomainModel()
    {
        var result = Types.InAssembly(AtendimentoApi)
            .ShouldNot()
            .HaveDependencyOnAny("Advocacia.Platform.Domain", "Advocacia.Platform.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void AtendimentoModule_ShouldNotDependOn_AnotherBusinessModule()
    {
        foreach (var assembly in new[] { AtendimentoDomain, AtendimentoApplication, AtendimentoInfrastructure, AtendimentoApi })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny("Advocacia.Modules.Legal", "Advocacia.Modules.Integrations")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {BuildFailureMessage(result)}");
        }
    }

    [Fact]
    public void AtendimentoConsumers_ShouldEndWithConsumer_AndInheritFromConsumerBase()
    {
        var consumers = Types.InAssembly(AtendimentoInfrastructure)
            .That().ImplementInterface(typeof(IConsumer<>));

        consumers.Should().HaveNameEndingWith("Consumer").GetResult().IsSuccessful.Should().BeTrue();

        // Idempotência (IProcessedEventStore), métrica e span vêm da base — um consumer que a
        // ignorasse reprocessaria a mesma mensagem a cada retry do MassTransit.
        var inheriting = Types.InAssembly(AtendimentoInfrastructure)
            .That().HaveNameEndingWith("Consumer").And().AreClasses()
            .Should().Inherit(typeof(ConsumerBase<>))
            .GetResult();

        inheriting.IsSuccessful.Should().BeTrue(BuildFailureMessage(inheriting));
    }

    [Fact]
    public void AtendimentoDomainEvents_NeverCarryPersonalData()
    {
        // Eventos atravessam a outbox e a mensageria. Telefone e texto da conversa são dado
        // pessoal e sigiloso profissional: o front recebe só ids e busca o resto pela API
        // autenticada (e protegida por RLS).
        var forbidden = new[] { "body", "phone", "telefone", "texto", "text", "message", "contactname", "nome" };

        var offenders = typeof(AtendimentoIniciadoEvent).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AtendimentoIniciadoEvent).Namespace)
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => forbidden.Any(word => pair.property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}")
            .ToList();

        offenders.Should().BeEmpty("eventos de domínio do Atendimento não podem carregar dado pessoal");
    }

    [Fact]
    public void AtendimentoApi_ExposesEndpointGroupsOnlyThroughMapExtensions()
    {
        var mapExtensions = Types.InAssembly(AtendimentoApi)
            .That().HaveNameEndingWith("Endpoints")
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal))
            .Select(method => method.Name)
            .ToList();

        mapExtensions.Should().Contain(["MapBotEndpoints", "MapAtendimentosEndpoints"]);
    }

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a convenção do módulo Atendimento: " +
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
