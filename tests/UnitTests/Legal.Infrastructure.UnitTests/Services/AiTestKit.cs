using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Infrastructure.Services;
using Advocacia.Modules.Workflow.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

/// <summary>Monta os serviços de IA com o catálogo de prompts REAL e a API da Anthropic falsa.</summary>
internal sealed class AiTestKit
{
    public const string ClassificationModel = "modelo-classificacao";
    public const string DeadlineModel = "modelo-prazo";

    public AiTestKit(int maxContentCharacters = 16000)
    {
        Client = Substitute.For<IAnthropicClient>();
        Recorder = new RecordingUsageRecorder();

        var cost = Substitute.For<IAiCostCalculator>();
        cost.Calculate(Arg.Any<string>(), Arg.Any<AnthropicUsage>()).Returns(0.01m);

        var models = Options.Create(new AnthropicModelsOptions
        {
            Classification = ClassificationModel,
            Deadline = DeadlineModel,
            MaxContentCharacters = maxContentCharacters,
        });

        var executor = new AiPromptExecutor(
            new EmbeddedPromptCatalog(), Client, cost, Recorder, models, NullLogger<AiPromptExecutor>.Instance);

        Classification = new ClassificationService(executor, models, NullLogger<ClassificationService>.Instance);
        Deadline = new DeadlineSuggestionService(executor, models, NullLogger<DeadlineSuggestionService>.Instance);
    }

    public IAnthropicClient Client { get; }

    public RecordingUsageRecorder Recorder { get; }

    public ClassificationService Classification { get; }

    public DeadlineSuggestionService Deadline { get; }

    public AnthropicRequest? LastRequest { get; private set; }

    public void RespondWith(string text, string model = "modelo-respondeu", AnthropicUsage? usage = null)
    {
        Client.SendAsync(Arg.Any<AnthropicRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                LastRequest = call.Arg<AnthropicRequest>();
                return Result.Success(new AnthropicResponse(
                    text, model, "end_turn", "req_1", usage ?? new AnthropicUsage(1000, 60, 0, 0)));
            });
    }

    public void FailWith(Error error)
    {
        Client.SendAsync(Arg.Any<AnthropicRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnthropicResponse>(error));
    }

    internal sealed class RecordingUsageRecorder : IAiUsageRecorder
    {
        public List<AiUsageEntry> Entries { get; } = [];

        public Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
