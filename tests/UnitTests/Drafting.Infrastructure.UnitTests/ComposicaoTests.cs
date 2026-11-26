using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Application.Validacao;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Normas;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

/// <summary>
/// O contêiner de verdade, com o appsettings versionado dos hosts: tudo resolve, nada de escopo capturado por
/// singleton, e a configuração chega às opções com os valores que o produto define.
/// </summary>
public class ComposicaoTests
{
    private static string Raiz()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Advocacia.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Advocacia.sln");
    }

    private static ServiceProvider Montar(string host, params (string Chave, string Valor)[] extras)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(Raiz(), "src", "Hosts", host, "appsettings.json"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost",
                ["Ollama:BaseUrl"] = "http://localhost:11434",
                ["Qdrant:BaseUrl"] = "http://localhost:6334",
            })
            .AddInMemoryCollection(extras.ToDictionary(e => e.Chave, e => (string?)e.Valor))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Substitute.For<IAlertPublisher>());
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        services.AddScoped(_ => Substitute.For<ICurrentUser>());
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<TenantContextInterceptor>();
        services.AddSingleton(Options.Create(ModuleInit.Chaves));
        services.AddDraftingInfrastructure(configuration);
        services.AddDraftingWorker();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public async Task TodosOsServicosDoModulo_Resolvem(string host)
    {
        await using var provider = Montar(host);
        await using var scope = provider.CreateAsyncScope();

        foreach (var tipo in new[]
                 {
                     typeof(ICasoService), typeof(IAnexoService), typeof(IConversorDocumentoService), typeof(IOcrService), typeof(ITranscricaoService),
                     typeof(IExtracaoMetadadosService), typeof(IClassificacaoDocumentoService), typeof(IChunkerJuridicoService), typeof(IEmbeddingService),
                     typeof(IQdrantService), typeof(IBuscaJuridicaService), typeof(IBuscaDocumentosCasoService), typeof(IRerankerService),
                     typeof(IPromptBuilder), typeof(IGeracaoPecaService), typeof(IValidacaoCitacaoService), typeof(IAuditoriaIaService),
                     typeof(IOllamaService), typeof(IVastAiService), typeof(IGpuOrchestrator), typeof(IGpuStateService), typeof(INotificacaoService),
                     typeof(IRevisaoPecaService), typeof(INormaSyncOrchestrator),
                 })
        {
            scope.ServiceProvider.GetService(tipo).Should().NotBeNull(tipo.Name);
        }

        scope.ServiceProvider.GetServices<IQueueWorkHandler>().Select(h => (h.Tipo, h.RequerGpu)).Should().BeEquivalentTo(
            [(QueueItemKind.Geracao, true), (QueueItemKind.Indexacao, true), (QueueItemKind.Conversao, false)]);
        scope.ServiceProvider.GetServices<IConversorFormato>().SelectMany(c => c.Formatos).Should()
            .BeEquivalentTo(Enum.GetValues<FormatoDocumento>(), "todo formato aceito no upload tem exatamente um conversor");
    }

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public void AppSettings_TrazOsValoresDoProduto(string host)
    {
        using var provider = Montar(host);

        var ollama = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;
        (ollama.Modelo, ollama.ModeloEmbedding, ollama.TimeoutSegundos, ollama.Temperatura, ollama.TopP, ollama.NumCtx)
            .Should().Be(("jurema-7b", "bge-m3", 600, 0.2, 0.9, 8192));
        var janela = provider.GetRequiredService<IOptions<JanelaModeloOptions>>().Value;
        (janela.NumCtx, janela.NumPredict).Should().Be((ollama.NumCtx, ollama.NumPredict), "o orçamento do contexto usa a mesma janela");
        provider.GetRequiredService<IOptions<OllamaModelsOptions>>().Value.Required.Should().BeEquivalentTo("jurema-7b", "bge-m3");
        provider.GetRequiredService<IOptions<VastAiOptions>>().Value.IdleTimeoutMinutes.Should().Be(10);
        provider.GetRequiredService<IOptions<DraftingOptions>>().Value.Enabled.Should().BeFalse("nasce desligado");
        provider.GetRequiredService<IOptions<NormasIngestaoOptions>>().Value.Incluidas.Should().Equal("constituicao.md", "federal", "jurisprudencia");
        provider.GetRequiredService<IRepositorySync>().Should().BeOfType<LocalDirectoryRepositorySync>("os Markdowns já foram baixados");
    }

    [Fact]
    public void ModoGit_EEscolhaDeConfiguracao()
    {
        using var provider = Montar("Worker", ("Ingestao:Modo", "Git"));

        provider.GetRequiredService<IRepositorySync>().Should().BeOfType<GitSparseRepositorySync>();
    }

    [Fact]
    public void AppSettingsVersionado_NaoTemSegredo()
    {
        foreach (var host in new[] { "Api", "Worker" })
        {
            var texto = File.ReadAllText(Path.Combine(Raiz(), "src", "Hosts", host, "appsettings.json"));

            foreach (Match match in Regex.Matches(texto, "\"(ApiKey|SecretKey|AccessKey|InstanceId)\"\\s*:\\s*\"([^\"]*)\""))
            {
                match.Groups[2].Value.Should().BeEmpty($"{match.Groups[1].Value} em {host}");
            }
        }
    }
}
