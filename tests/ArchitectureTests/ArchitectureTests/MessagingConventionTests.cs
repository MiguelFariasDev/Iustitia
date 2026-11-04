using System.Reflection;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using FluentAssertions;
using MassTransit;
using NetArchTest.Rules;

namespace Advocacia.ArchitectureTests;

/// <summary>Convenções de nomenclatura de mensageria e jobs (Etapa 0.5 — ver ADR-016/017).</summary>
public class MessagingConventionTests
{
    private static readonly Assembly PlatformInfrastructure = typeof(OutboxProcessorJob).Assembly;

    [Fact]
    public void Consumers_ShouldHaveNameEndingWith_Consumer()
    {
        var result = Types.InAssembly(PlatformInfrastructure)
            .That().ImplementInterface(typeof(IConsumer<>))
            .Should().HaveNameEndingWith("Consumer")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Consumers_ShouldInheritFromConsumerBase()
    {
        var result = Types.InAssembly(PlatformInfrastructure)
            .That().HaveNameEndingWith("Consumer")
            .And().AreClasses()
            .Should().Inherit(typeof(ConsumerBase<>))
            .Or().Inherit(typeof(IntegrationEventConsumer<>))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Jobs_ShouldHaveNameEndingWith_Job()
    {
        var result = Types.InAssembly(PlatformInfrastructure)
            .That().ResideInNamespace("Advocacia.Platform.Infrastructure.Jobs.Jobs")
            .And().AreClasses()
            .And().DoNotHaveNameEndingWith("Options")
            .Should().HaveNameEndingWith("Job")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    private static string BuildFailureMessage(TestResult result) =>
        "Tipos violando a convenção de nomenclatura: " +
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
