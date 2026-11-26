using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Application.Validacao;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Pecas;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class PecaProcessorTests
{
    private const string MinutaBoa =
        "DOS FATOS\nO autor contratou o serviço.\nDO DIREITO\nHá ato ilícito [Código Civil, art. 186], com multa contratual [contrato.pdf, p. 3].\n" +
        "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir os valores.";

    private sealed class Harness
    {
        public FakeRepository Repository { get; } = new();

        public FakeQueue Queue { get; }

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public Mundo Mundo { get; } = new();

        public FakeClock Clock => Mundo.Clock;

        public FakeNotificacao Notificacao { get; } = new();

        public bool ComAnexo { get; init; } = true;

        public VectorHit[] Juridico { get; init; } = [Samples.HitNorma()];

        public ILlmEstruturado Extracao { get; init; } = new FakeEstruturado(falha: new LlmServiceException("sem extração"));

        public FakeLlm Llm { get; init; } = new(
        [
            "DOS FATOS\nO autor contratou o serviço.\n",
            "DO DIREITO\nHá ato ilícito [Código Civil, art. 186], com multa contratual [contrato.pdf, p. 3].\n",
            "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir os valores.",
        ]);

        public int MaxTentativas { get; init; } = 3;

        public Harness() => Queue = new FakeQueue(Mundo.Clock);

        public PecaProcessor Processor { get; private set; } = null!;

        public ILegalDocumentService Documents { get; private set; } = null!;

        public Harness Build()
        {
            if (ComAnexo)
            {
                Mundo.NovoDocumento();
            }

            var user = new FakeCurrentUser();
            Documents = new LegalDocumentService(Repository, Queue, UnitOfWork, user, Clock, Options.Create(new CustoOptions { GpuUsdPorHora = 0.40m }));
            var audit = new AuditoriaIaService(Repository, UnitOfWork, Clock, NullLogger<AuditoriaIaService>.Instance);
            var busca = Options.Create(new BuscaNormasOptions());
            var rag = new RagService(
                new ConsultaEnriquecidaService(Extracao, NullLogger<ConsultaEnriquecidaService>.Instance),
                new FakeEmbeddings(),
                new BuscaDocumentosCasoService(
                    Mundo.Vetores, Mundo.Documentos, new RerankerService(), Mundo.Seguranca, busca, NullLogger<BuscaDocumentosCasoService>.Instance),
                new FakeBuscaJuridica(Juridico),
                Options.Create(new RagOptions()), busca, Options.Create(new JanelaModeloOptions()));

            Processor = new PecaProcessor(
                Repository, Documents, rag, new PromptBuilder(), Llm, audit, Notificacao,
                Options.Create(new FilaOptions { MaxTentativas = MaxTentativas, ParcialIntervaloMs = 0 }),
                Mundo.Casos, Mundo.Documentos, new ValidacaoCitacaoService(new MemNormaStore()), NullLogger<PecaProcessor>.Instance);

            return this;
        }

        public Task<LegalDocumentRequest> NewRequestAsync(string? fatos = null) => NewRequestAsync(Input(fatos));

        public PecaInput Input(string? fatos = null) =>
            (fatos is null ? Samples.Input() : Samples.Input(fatos)) with { CasoId = Mundo.Caso.Id.Value, ClienteId = Mundo.Caso.ClienteId };

        public async Task<LegalDocumentRequest> NewRequestAsync(PecaInput input) => (await Documents.CreateAndEnqueueAsync(input)).Value;
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
        request.ModeloUtilizado.Should().Be("jurema-7b");
        request.PromptVersao.Should().Be(SystemPrompt.Versao);
        request.TokensEntrada.Should().Be(120);
        request.TokensSaida.Should().Be(340);
        request.TempoGeracaoMs.Should().NotBeNull();
        request.CustoEstimadoUsd.Should().NotBeNull();
        request.ConcluidoEm.Should().NotBeNull();
        request.Tentativas.Should().Be(1);
        h.Repository.Sources.Should().HaveCount(2).And.OnlyContain(f => f.UtilizadaNaGeracao);
        h.Repository.Citacoes.Should().HaveCount(2).And.OnlyContain(c => c.Status == CitacaoStatus.Validada);
        request.ConsultaJson.Should().Contain("heuristica");
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
        mensagens[1].Content.Should().Contain("[contrato.pdf, p. 3]").And.Contain("[Código Civil, art. 186]").And.Contain("Cláusula 5: multa de 10%");
        h.Mundo.Vetores.Filtros.Should().NotBeEmpty().And.OnlyContain(f =>
            f.TenantId == Ids.Tenant && f.CasoId == h.Mundo.Caso.Id.Value && f.UsuarioId == Ids.User);
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
    public async Task EtapasDaBusca_SaoAsDoContratoDeStreaming()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync();
        var etapas = new List<string>();
        h.UnitOfWork.OnSave = () => etapas.Add($"{request.Status}|{request.EtapaAtual}");

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        etapas.Distinct().Should().ContainInOrder(
            "BuscandoDocumentosCaso|Analisando anexos do caso.",
            "BuscandoJuridico|Buscando fundamentos jurídicos.",
            "Gerando|Gerando minuta.",
            "Validando|Conferindo as citações da minuta.",
            "Salvando|Salvando a minuta.");
    }

    [Fact]
    public async Task SolicitacaoSemCaso_FalhaSemTocarNoModelo()
    {
        var h = new Harness().Build();
        var request = await h.NewRequestAsync(Samples.Input() with { CasoId = Guid.NewGuid() });

        var outcome = await h.Processor.ProcessarAsync(request.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Falhou);
        h.Llm.Calls.Should().Be(0);
        h.Mundo.Vetores.Filtros.Should().BeEmpty();
    }

    [Fact]
    public async Task AnexoDeOutroCaso_NuncaChegaAoPrompt_MesmoQueOIndiceDevolva()
    {
        var h = new Harness().Build();
        h.Mundo.NovoDocumento(h.Mundo.NovoCaso(), "segredo.pdf", "SEGREDO DE OUTRO CLIENTE");
        h.Mundo.Vetores.IgnorarFiltro = true;
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        h.Llm.LastRequest!.Messages[1].Content.Should().NotContain("SEGREDO").And.Contain("Cláusula 5");
        h.Mundo.Seguranca.Eventos.Should().Contain(e => e.Tipo == "TrechoForaDoCaso");
    }

    [Fact]
    public async Task DocumentoRestritoDeOutraPessoa_NaoEntraNaMinutaDeQuemNaoPodeVer()
    {
        var h = new Harness().Build();
        var colega = Guid.NewGuid();
        h.Mundo.Caso.Conceder(colega, CasoPermissao.Leitura, Ids.User, h.Clock.UtcNow);
        h.Mundo.NovoDocumento(nome: "restrito.pdf", texto: "CONTEÚDO RESTRITO", restrito: true);
        var criada = LegalDocumentRequest.Create(
            Ids.Tenant, colega, "peticao_inicial", "civel", "Fatos.", "Pedidos.", null, null, h.Clock.UtcNow,
            casoId: h.Mundo.Caso.Id.Value).Value;
        h.Repository.Add(criada);

        await h.Processor.ProcessarAsync(criada.Id.Value, 1);

        h.Llm.LastRequest!.Messages[1].Content.Should().NotContain("RESTRITO");
        h.Mundo.Vetores.Filtros.Should().OnlyContain(f => !f.VeRestritos);
    }

    [Fact]
    public async Task AnexoPendenteEAnexoComFalha_ViramAvisoNaMinuta()
    {
        var h = new Harness().Build();
        h.Mundo.Documentos.Itens.Add(CasoDocumento.Criar(
            CasoDocumentoId.New(), h.Mundo.Caso, Ids.User, "novo.pdf", FormatoDocumento.Pdf, 1, "h1", "k", false, h.Clock.UtcNow));
        var falho = CasoDocumento.Criar(
            CasoDocumentoId.New(), h.Mundo.Caso, Ids.User, "ruim.pdf", FormatoDocumento.Pdf, 1, "h2", "k", false, h.Clock.UtcNow);
        falho.MarcarFalha("x");
        h.Mundo.Documentos.Itens.Add(falho);
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        Avisos(request).Should().Contain(a => a.Contains("ainda estavam sendo processados")).And.Contain(a => a.Contains("não puderam ser lidos"));
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
        var h = new Harness { ComAnexo = false, Juridico = [] }.Build();
        var request = await h.NewRequestAsync();

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        Avisos(request).Should().Contain(a => a.Contains("Nenhum trecho dos anexos")).And.Contain(a => a.Contains("Nenhuma norma ou decisão"));
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

        var avisos = string.Join('\n', Avisos(request));
        avisos.Should().Contain("inventado.pdf, p. 9").And.Contain("art. 927").And.Contain("Súmula nº 297").And.Contain("DO DIREITO");
        h.Repository.Sources.Should().OnlyContain(f => !f.UtilizadaNaGeracao);
        h.Repository.Citacoes.Should().Contain(c => c.Status == CitacaoStatus.PossivelAlucinacao && c.Texto.Contains("inventado"));
        h.Repository.Citacoes.Should().Contain(c => c.Status == CitacaoStatus.PendenteDeVerificacao && c.Texto.Contains("927"));
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
        var h = new Harness().Build();
        h.Mundo.Vetores.ForaDoAr = true;
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
        var request = await h.NewRequestAsync("FATO-SIGILOSO-123");

        await h.Processor.ProcessarAsync(request.Id.Value, 1);

        var tudo = string.Join('\n', h.Repository.Events.Select(e => e.Tipo + e.DetalheJson));
        tudo.Should().NotContain("FATO-SIGILOSO-123").And.NotContain("multa de 10%").And.NotContain("ato ilícito");
    }

    private static List<string> Avisos(LegalDocumentRequest request) =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(request.AvisosJson ?? "[]")!;
}
