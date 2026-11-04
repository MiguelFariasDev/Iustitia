using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Infrastructure.Services;
using Advocacia.Modules.Workflow.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

/// <summary>Monta os serviços de IA com o catálogo de prompts REAL e o cliente da Anthropic falso.</summary>
internal sealed class AiTestKit
{
    public const string ClassificationModel = "modelo-classificacao";
    public const string DeadlineModel = "modelo-prazo";

    public AiTestKit(int maxContentCharacters = 16000)
    {
        Client = Substitute.For<IAnthropicClient>();
        Recorder = new RecordingUsageRecorder();

        var models = Options.Create(new AnthropicModelsOptions
        {
            Classification = ClassificationModel,
            Deadline = DeadlineModel,
            MaxContentCharacters = maxContentCharacters,
        });

        var executor = new AiPromptExecutor(
            new EmbeddedPromptCatalog(), Client, Recorder, models, NullLogger<AiPromptExecutor>.Instance);

        Classification = new ClassificationService(executor, models, NullLogger<ClassificationService>.Instance);
        Deadline = new DeadlineSuggestionService(executor, models, NullLogger<DeadlineSuggestionService>.Instance);
    }

    public IAnthropicClient Client { get; }

    public RecordingUsageRecorder Recorder { get; }

    public ClassificationService Classification { get; }

    public DeadlineSuggestionService Deadline { get; }

    public CapturedCall? LastRequest { get; private set; }

    public void RespondWith(string text, string model = "modelo-respondeu", AnthropicUsage? usage = null)
    {
        Client.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AnthropicOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                LastRequest = new CapturedCall(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<AnthropicOptions>(2));
                return Result.Success(new AnthropicResponse(
                    "msg_1", model, text, usage ?? new AnthropicUsage(1000, 60, 0, 0), "end_turn", [], "req_1"));
            });
    }

    public void FailWith(Error error)
    {
        Client.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AnthropicOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnthropicResponse>(error));
    }

    /// <summary>O que o cliente recebeu: facilita asserções sobre o prompt e as opções.</summary>
    internal sealed record CapturedCall(string UserPrompt, string SystemPrompt, AnthropicOptions Options)
    {
        public string? Model => Options.Model;

        public bool CacheSystemPrompt => Options.CacheSystemPrompt;
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
