using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.Movimentacoes;

/// <summary>Seção 6 do contrato: base sem aviso, novidade com alerta e rascunho, acúmulo, cliente sem e-mail.</summary>
public sealed class SincronizarMovimentacoesHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Responsavel = Guid.NewGuid();

    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly MovimentacoesEmMemoria _movimentacoes = new();
    private readonly AvisosEmMemoria _avisos = new();
    private readonly IAcompanhamentoProcessual _acompanhamento = Substitute.For<IAcompanhamentoProcessual>();
    private readonly IAlertPublisher _alertas = Substitute.For<IAlertPublisher>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _relogio = Substitute.For<IDateTimeProvider>();
    private DateTimeOffset _agora = new(2026, 11, 25, 5, 30, 0, TimeSpan.Zero);

    public SincronizarMovimentacoesHandlerTests()
    {
        _relogio.UtcNow.Returns(_ => _agora);
        _alertas.PublishAsync(Arg.Any<AlertRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        _usuario.UserId.Returns(Responsavel);
    }

    private SincronizarMovimentacoesHandler Handler() => new(
        _processos, _clientes, _movimentacoes, _avisos, _acompanhamento, _alertas, _unitOfWork, _usuario, _relogio);

    private Processo NovoProcesso(string? emailDoCliente = "cliente@exemplo.com.br", bool confidencial = false)
    {
        var cliente = Cliente.Create(
            Tenant, TipoCliente.PessoaFisica, NomeCliente.Create("Maria da Silva").Value,
            Documento.Create("11144477735", TipoCliente.PessoaFisica).Value,
            Contato.Create(emailDoCliente, null).Value).Value;
        _clientes.GetByIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        var processo = Processo.Create(
            Tenant, CNJNumber.Create("1234567-48.2026.8.26.0001").Value, cliente.Id.Value, Responsavel, AreaDireito.Civel).Value;
        if (confidencial)
        {
            processo.DefinirConfidencialidade(true);
        }

        _processos.GetByIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return processo;
    }

    private void TribunalDevolve(params AndamentoCapturado[] andamentos) =>
        _acompanhamento.ObterAsync(Arg.Any<CNJNumber>(), Arg.Any<string?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AndamentosCapturados(true, FonteConsulta.Juscraper, true, andamentos)));

    private static AndamentoCapturado Andamento(string id, string nome, int dia) =>
        new(id, nome, null, new DateTimeOffset(2026, 11, dia, 0, 0, 0, TimeSpan.FromHours(-3)), "G1", false);

    [Fact]
    public async Task PrimeiraSincronizacao_GravaABase_SemAlertaNemAviso()
    {
        var processo = NovoProcesso();
        TribunalDevolve(Andamento("a", "Distribuído", 10), Andamento("b", "Citação", 12));

        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        resultado.Value.Base.Should().BeTrue();
        resultado.Value.Novos.Should().Be(2);
        _movimentacoes.Itens.Should().HaveCount(2).And.OnlyContain(m => m.Fonte == "juscraper");
        _avisos.Itens.Should().BeEmpty();
        await _alertas.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
        await _acompanhamento.Received(1).ObterAsync(Arg.Any<CNJNumber>(), null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AndamentoNovo_GeraAlertaAoResponsavel_ERascunhoParaRevisao()
    {
        var processo = NovoProcesso();
        TribunalDevolve(Andamento("a", "Distribuído", 10));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        _agora = _agora.AddDays(1);
        TribunalDevolve(Andamento("a", "Distribuído", 10), Andamento("b", "Sentença", 24));
        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        resultado.Value.Should().BeEquivalentTo(new { Base = false, Novos = 1, Alertou = true, AvisoAtualizado = true });
        var aviso = _avisos.Itens.Single();
        aviso.Status.Should().Be(StatusNotificacaoCliente.PendenteRevisao, "o job nunca envia: o advogado revisa");
        aviso.Mensagem.Should().Contain("Sentença").And.Contain("1234567-48.2026.8.26.0001");
        await _alertas.Received(1).PublishAsync(
            Arg.Is<AlertRequest>(a => a.UserId == Responsavel && a.Type == "Movimentacao"
                && a.Description!.Contains("1 andamento(s)") && !a.Description.Contains("Sentença")),
            Arg.Any<CancellationToken>());
        // Janela: último andamento (10/11) menos 3 dias.
        await _acompanhamento.Received(1).ObterAsync(
            Arg.Any<CNJNumber>(), "juscraper", new DateOnly(2026, 11, 7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RascunhoPendente_AcumulaOsAndamentosDasNoitesSeguintes()
    {
        var processo = NovoProcesso();
        TribunalDevolve(Andamento("a", "Distribuído", 10));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        _agora = _agora.AddDays(1);
        TribunalDevolve(Andamento("b", "Citação", 20));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        _agora = _agora.AddDays(1);
        TribunalDevolve(Andamento("c", "Contestação", 22));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        var aviso = _avisos.Itens.Should().ContainSingle().Subject;
        aviso.QuantidadeAndamentos.Should().Be(2);
        aviso.Mensagem.Should().Contain("Citação").And.Contain("Contestação").And.NotContain("Distribuído");
    }

    [Fact]
    public async Task ClienteSemEmail_SoAlerta()
    {
        var processo = NovoProcesso(emailDoCliente: null);
        TribunalDevolve(Andamento("a", "Distribuído", 10));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        TribunalDevolve(Andamento("b", "Sentença", 24));
        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        resultado.Value.Alertou.Should().BeTrue();
        resultado.Value.AvisoAtualizado.Should().BeFalse();
        _avisos.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task AndamentoSemId_EIgnorado()
    {
        var processo = NovoProcesso();
        TribunalDevolve(Andamento("a", "Distribuído", 10), new AndamentoCapturado(null, "Sem id", null, null, null, false));

        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        resultado.Value.Novos.Should().Be(1);
    }

    [Fact]
    public async Task TrocaExplicitaDeFonte_GravaNovaBase_SemNotificar()
    {
        var processo = NovoProcesso();
        TribunalDevolve(Andamento("a", "Distribuído", 10));
        await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        _agora = _agora.AddDays(1);
        _acompanhamento.ObterAsync(Arg.Any<CNJNumber>(), "datajud", null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AndamentosCapturados(true, FonteConsulta.DataJud, true,
                [new AndamentoCapturado("datajud:abc", "Distribuição", null, null, "G1", false)])));

        var resultado = await Handler().Handle(
            new SincronizarMovimentacoesCommand(processo.Id.Value, TrocarParaFonte: "datajud"), default);

        resultado.Value.Base.Should().BeTrue();
        _avisos.Itens.Should().BeEmpty();
        await _alertas.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [Fact]
    public async Task ProcessoConfidencial_SobDemanda_SoParaQuemPodeVer()
    {
        var processo = NovoProcesso(confidencial: true);
        _usuario.UserId.Returns(Guid.NewGuid());
        _usuario.IsInRole(Arg.Any<string>()).Returns(false);

        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value), default);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_NOT_FOUND));
        await _acompanhamento.DidNotReceiveWithAnyArgs().ObterAsync(default!, default, default, default);
    }

    [Fact]
    public async Task FalhaDaFonte_EPropagada_SemGravarNada()
    {
        var processo = NovoProcesso();
        _acompanhamento.ObterAsync(Arg.Any<CNJNumber>(), Arg.Any<string?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AndamentosCapturados>(ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE)));

        var resultado = await Handler().Handle(new SincronizarMovimentacoesCommand(processo.Id.Value, OrigemJob: true), default);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_UNAVAILABLE));
        _movimentacoes.Itens.Should().BeEmpty();
    }
}
