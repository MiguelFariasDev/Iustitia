using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;
using Advocacia.Modules.Legal.Application.UnitTests.Features.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.NotificacoesCliente;

/// <summary>Revisão humana obrigatória: só sai e-mail quando o advogado manda, e o HTML sai escapado.</summary>
public sealed class EnviarNotificacaoClienteHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Advogado = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 11, 25, 14, 0, 0, TimeSpan.Zero);

    private readonly AvisosEmMemoria _avisos = new();
    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _relogio = Substitute.For<IDateTimeProvider>();

    public EnviarNotificacaoClienteHandlerTests()
    {
        _usuario.UserId.Returns(Advogado);
        _relogio.UtcNow.Returns(Agora);
    }

    private (NotificacaoCliente Aviso, Cliente Cliente) Cenario(string? email = "cliente@exemplo.com.br")
    {
        var cliente = Cliente.Create(
            Tenant, TipoCliente.PessoaFisica, NomeCliente.Create("Maria da Silva").Value,
            Documento.Create("11144477735", TipoCliente.PessoaFisica).Value, Contato.Create(email, null).Value).Value;
        _clientes.GetByIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        var processo = Processo.Create(
            Tenant, CNJNumber.Create("1234567-48.2026.8.26.0001").Value, cliente.Id.Value, Advogado, AreaDireito.Civel).Value;
        _processos.GetByIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        var aviso = NotificacaoCliente.CriarRascunho(
            Tenant, processo.Id.Value, cliente.Id.Value, processo.CnjNumber.Value,
            [new AndamentoDoAviso(Agora, "Sentença <b>procedente</b>")], Agora);
        _avisos.Add(aviso);

        return (aviso, cliente);
    }

    private EnviarNotificacaoClienteHandler Enviar() =>
        new(_avisos, _processos, _clientes, _email, _unitOfWork, _usuario, _relogio, NullLogger<EnviarNotificacaoClienteHandler>.Instance);

    [Fact]
    public async Task Enviar_MandaOTextoRevisado_ComHtmlEscapado_ERegistraORevisor()
    {
        var (aviso, _) = Cenario();

        var resultado = await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value, "Olá!\n<script>x</script>"), default);

        resultado.IsSuccess.Should().BeTrue();
        aviso.Status.Should().Be(StatusNotificacaoCliente.Enviada);
        aviso.RevisadoPor.Should().Be(Advogado);
        aviso.EnviadoEm.Should().Be(Agora);
        await _email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "cliente@exemplo.com.br"
                && m.Html.Contains("&lt;script&gt;x&lt;/script&gt;") && !m.Html.Contains("<script>")
                && m.Text == "Olá!\n<script>x</script>"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SemEdicao_EnviaORascunho_EscapandoOTextoDoCartorio()
    {
        var (aviso, _) = Cenario();

        await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value), default);

        await _email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Html.Contains("Sentença &lt;b&gt;procedente&lt;/b&gt;")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FalhaDoProvedor_DeixaComoFalhaEnvio_QuePodeSerReenviada()
    {
        var (aviso, _) = Cenario();
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("503"));

        var falhou = await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value), default);

        falhou.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CLIENT_NOTIFICATION_SEND_FAILED));
        aviso.Status.Should().Be(StatusNotificacaoCliente.FalhaEnvio);

        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var reenviado = await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value), default);

        reenviado.IsSuccess.Should().BeTrue();
        aviso.Status.Should().Be(StatusNotificacaoCliente.Enviada);
    }

    [Fact]
    public async Task Descartado_NaoChega_ENaoPodeMaisSerEnviado()
    {
        var (aviso, _) = Cenario();

        var descartado = await new DescartarNotificacaoClienteHandler(_avisos, _processos, _unitOfWork, _usuario, _relogio)
            .Handle(new DescartarNotificacaoClienteCommand(aviso.Id.Value), default);
        var depois = await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value), default);

        descartado.Value.Status.Should().Be("Descartada");
        depois.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_PENDING));
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task ClienteQuePerdeuOEmail_NaoRecebe()
    {
        var (aviso, _) = Cenario(email: null);

        var resultado = await Enviar().Handle(new EnviarNotificacaoClienteCommand(aviso.Id.Value), default);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CLIENT_NOTIFICATION_RECIPIENT_UNAVAILABLE));
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }
}
