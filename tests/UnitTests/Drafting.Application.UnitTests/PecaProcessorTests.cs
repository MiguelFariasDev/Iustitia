using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Pecas;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class PecaProcessorTests
{
    private const string MinutaBoa =
        "DOS FATOS\nO autor contratou o serviço.\nDO DIREITO\nHá ato ilícito [contrato.pdf, p. 3].\n" +
        "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir os valores.";

    private sealed class Harness
    {
        public FakeRepository Repository { get; } = new();

        public FakeQueue Queue { get; }

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeClock Clock { get; } = new();

        public FakeNotificacao Notificacao { get; } = new();

        public FakeVectorStore Store { get; init; } = new([Samples.Hit("pt-1", "Art. 186: ato ilícito.", 0.91)]);

        public FakeLlm Llm { get; init; } = new(
        [
            "DOS FATOS\nO autor contratou o serviço.\n",
            "DO DIREITO\nHá ato ilícito [contrato.pdf, p. 3].\n",
            "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir os valores.",
        ]);

        public int MaxTentativas { get; init; } = 3;

        public Harness() => Queue = new FakeQueue(Clock);

        public PecaProcessor Processor { get; private set; } = null!;

        public ILegalDocumentService Documents { get; private set; } = null!;

        public Harness Build()
        {
            var user = new FakeCurrentUser();
            Documents = new LegalDocumentService(Repository, Queue, UnitOfWork, user, Clock, Options.Create(new CustoOptions { GpuUsdPorHora = 0.40m }));
            var audit = new DraftingAuditService(Repository, UnitOfWork, Clock, NullLogger<DraftingAuditService>.Instance);
            var rag = new RagService(new FakeEmbeddings(), Store, Options.Create(new RagOptions()), NullLogger<RagService>.Instance);

            Processor = new PecaProcessor(
                Repository, Documents, rag, new PromptBuilder(), Llm, audit, Notificacao,
                Options.Create(new FilaOptions { MaxTentativas = MaxTentativas, ParcialIntervaloMs = 0 }), Clock,
                NullLogger<PecaProcessor>.Instance);

            return this;
        }

        public async Task<LegalDocumentRequest> NewRequestAsync()
        {
            var created = await Documents.CreateAndEnqueueAsync(Samples.Input());

            return created.Value;
        }
    }

    [Fact]
    public async Task CaminhoFeliz_ConcluiPersisteTextoFontesTokensCustoEEventos()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        await h.Processor.AoReservarAsync(request.Id.Value);

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, tentativa: 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Concluido);
        request.Status.Should().Be(LegalDocumentStatus.Concluido);
        request.TextoGerado.Should().Be(MinutaBoa);
        request.Progresso.Should().Be(100);
        request.ModeloUtilizado.Should().Be("qwen3:8b");
        request.PromptVersao.Should().Be(SystemPrompt.Versao);
        request.TokensEntrada.Should().Be(120);
        request.TokensSaida.Should().Be(340);
        request.TempoGeracaoMs.Should().NotBeNull();
        request.CustoEstimadoUsd.Should().NotBeNull();
        request.ConcluidoEm.Should().NotBeNull();
        request.Tentativas.Should().Be(1);
        h.Repository.Sources.Should().ContainSingle().Which.UtilizadaNaGeracao.Should().BeTrue();
        h.Repository.Events.Select(e => e.Tipo)
            .Should().Equal("Reservada", "ContextoRecuperado", "GeracaoIniciada", "GeracaoConcluida");
        h.Notificacao.Eventos.Should().ContainSingle().Which.Should().StartWith("concluida:");
    }

    [Fact]
    public async Task EnviaAoModeloOSystemPromptFixoEOContextoRecuperado_ComOTenantDaSolicitacao()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        var mensagens = h.Llm.LastRequest!.Messages;
        mensagens.Select(m => m.Role).Should().Equal("system", "user");
        mensagens[0].Content.Should().Be(SystemPrompt.Texto);
        mensagens[1].Content.Should().Contain("[contrato.pdf, p. 3]").And.Contain("Art. 186: ato ilícito.");
        h.Store.LastQuery!.TenantId.Should().Be(Ids.Tenant);
        h.Store.LastQuery.UsuarioId.Should().Be(Ids.User);
    }

    [Fact]
    public async Task EtapasEProgressoAvancamEmOrdem()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        await h.Processor.AoReservarAsync(request.Id.Value);
        request.Status.Should().Be(LegalDocumentStatus.NaFila);
        request.Progresso.Should().Be(5);

        await h.Processor.AoPrepararGpuAsync(request.Id.Value);
        request.Status.Should().Be(LegalDocumentStatus.PreparandoGpu);
        request.EtapaAtual.Should().Contain("ambiente de IA");
    }

    [Fact]
    public async Task GravaOTextoParcialDuranteAGeracao_ParaOStreamLer()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        var parciais = new List<string?>();
        h.UnitOfWork.OnSave = () => parciais.Add(request.TextoGerado);

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        // ParcialIntervaloMs = 0: cada pedaço vira um envio parcial, crescente, antes do texto final.
        parciais.Where(p => p is not null).Should().Contain(p => p!.StartsWith("DOS FATOS") && !p.Contains("PONTOS PARA"));
    }

    [Fact]
    public async Task SemTrechosRecuperados_AvisaQueNaoHaBaseDocumental()
    {
        var h = new Harness { Store = new FakeVectorStore() }.Build();
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        request.AvisosJson.Should().Contain("Nenhum trecho relevante");
        h.Repository.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task CitacaoInventadaEReferenciaSemRespaldo_ViramAvisosPersistidos()
    {
        var llm = new FakeLlm(
        [
            "DO DIREITO\nVeja [inventado.pdf, p. 9] e o art. 927 do CC e a Súmula 297.\n" +
            "FONTES UTILIZADAS\nx\nPONTOS PARA REVISÃO HUMANA\ny",
        ]);
        var h = new Harness { Llm = llm }.Build();
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        request.AvisosJson.Should().Contain("inventado.pdf, p. 9").And.Contain("art. 927").And.Contain("Súmula 297");
        request.AvisosJson.Should().Contain("não cita nenhuma das fontes");
        h.Repository.Sources.Single().UtilizadaNaGeracao.Should().BeFalse();
    }

    [Fact]
    public async Task FalhaTransitoriaComTentativasSobrando_VoltaAFilaESemNotificarFalha()
    {
        var llm = new FakeLlm(["DOS FATOS\nParte do texto."]) { FailAfter = new LlmServiceException("O Ollama respondeu HTTP 500.") };
        var h = new Harness { Llm = llm }.Build();
        var request = await h.NewRequestAsync();

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, tentativa: 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Reagendar);
        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.TextoGerado.Should().BeNull();
        request.Erro.Should().Contain("LlmServiceException");
        h.Notificacao.Eventos.Should().BeEmpty();
        h.Repository.Events.Select(e => e.Tipo).Should().Contain("Reagendada");
    }

    [Fact]
    public async Task FalhaTransitoriaNaUltimaTentativa_EhFalhaDefinitivaComTextoParcialENotificacao()
    {
        var llm = new FakeLlm(["DOS FATOS\nParte do texto."]) { FailAfter = new TimeoutException("prazo") };
        var h = new Harness { Llm = llm, MaxTentativas = 3 }.Build();
        var request = await h.NewRequestAsync();

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, tentativa: 3);

        outcome.Kind.Should().Be(WorkOutcomeKind.Falhou);
        request.Status.Should().Be(LegalDocumentStatus.Falhou);
        request.TextoGerado.Should().Be("DOS FATOS\nParte do texto.");
        request.Erro.Should().Contain("TimeoutException");
        h.Notificacao.Eventos.Should().ContainSingle().Which.Should().StartWith("falha:");
    }

    [Fact]
    public async Task ErroPermanente_NaoEhRepetido_MesmoNaPrimeiraTentativa()
    {
        var llm = new FakeLlm(["x"]) { FailAfter = new InvalidOperationException("bug") };
        var h = new Harness { Llm = llm }.Build();
        var request = await h.NewRequestAsync();

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, tentativa: 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Falhou);
        request.Status.Should().Be(LegalDocumentStatus.Falhou);
    }

    [Fact]
    public async Task ModeloQueTerminaSemTexto_ETransitorio()
    {
        var h = new Harness { Llm = new FakeLlm([]) }.Build();
        var request = await h.NewRequestAsync();

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Reagendar);
    }

    [Fact]
    public async Task FalhaNoBancoVetorial_ETransitoria()
    {
        var h = new Harness { Store = new ThrowingStore() }.Build();
        var request = await h.NewRequestAsync();

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Reagendar);
        h.Llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SolicitacaoJaCancelada_NaoGastaGpu()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        await h.Documents.CancelAsync(request);

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Cancelado);
        h.Llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CancelamentoNoMeioDaGeracao_InterrompeAConexaoComOModelo()
    {
        var llm = new FakeLlm(["DOS FATOS\nComeço, ", "mais um pedaço, ", "e outro."]);
        var h = new Harness { Llm = llm }.Build();
        var request = await h.NewRequestAsync();
        // O usuário cancela assim que o primeiro texto parcial é gravado.
        h.UnitOfWork.OnSave = () =>
        {
            if (request.TextoGerado is not null && request.Status != LegalDocumentStatus.Cancelado)
            {
                request.Cancel(h.Clock.UtcNow);
            }
        };

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Cancelado);
        request.Status.Should().Be(LegalDocumentStatus.Cancelado);
        h.Notificacao.Eventos.Should().BeEmpty();
    }

    [Fact]
    public async Task DesligamentoDoServico_DevolveAFilaSemGastarTentativa()
    {
        var h = new Harness { Llm = new FakeLlm(["DOS FATOS\nComeço."]) { HangAfter = true } }.Build();
        var request = await h.NewRequestAsync();
        await h.Processor.AoReservarAsync(request.Id.Value);
        using var cts = new CancellationTokenSource();
        var task = h.Processor.ProcessarAsync(request.Id.Value, 1, cts.Token);
        await Task.Delay(100);

        await cts.CancelAsync();
        var outcome = await task;

        outcome.Kind.Should().Be(WorkOutcomeKind.Interrompido);
        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.Tentativas.Should().Be(0); // a reserva consumiu 1; a devolução reembolsa
    }

    [Fact]
    public async Task GpuIndisponivel_VoltaAEsperaSemGastarTentativa()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        await h.Processor.AoReservarAsync(request.Id.Value);
        request.Tentativas.Should().Be(1);

        await h.Processor.AoFaltarInfraAsync(request.Id.Value, "GPU indisponível");

        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.Tentativas.Should().Be(0);
        request.EtapaAtual.Should().Contain("Aguardando");
    }

    [Fact]
    public async Task Expirada_FalhaSemPassarPelaGpuENotifica()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();

        await h.Processor.AoExpirarAsync(request.Id.Value);

        request.Status.Should().Be(LegalDocumentStatus.Falhou);
        request.Erro.Should().Contain("Tempo máximo de espera");
        h.Llm.Calls.Should().Be(0);
        h.Notificacao.Eventos.Should().ContainSingle().Which.Should().StartWith("falha:");
    }

    [Fact]
    public async Task SolicitacaoInexistente_EhFalhaDoItem()
    {
        var h = new Harness().Build();

        var outcome = await h.Processor.ProcessarAsync(Guid.NewGuid(), 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Falhou);
    }

    [Fact]
    public async Task Trilha_NuncaGravaFatosPedidosNemTrechos()
    {
        var h = new Harness().Build();
        var created = await h.Documents.CreateAndEnqueueAsync(Samples.Input("FATO-SIGILOSO-123"));

        await h.Processor.ProcessarAsync(created.Value.Id.Value, 1);

        var tudo = string.Join('\n', h.Repository.Events.Select(e => e.Tipo + e.DetalheJson));
        tudo.Should().NotContain("FATO-SIGILOSO-123").And.NotContain("Art. 186").And.NotContain("ato ilícito");
    }

    private sealed class ThrowingStore : IVectorStore
    {
        public Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Models.VectorHit>> SearchAsync(Models.VectorSearchQuery query, CancellationToken cancellationToken = default) =>
            throw new VectorStoreException("qdrant fora do ar");

        public Task UpsertAsync(IReadOnlyList<Models.VectorPoint> points, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
