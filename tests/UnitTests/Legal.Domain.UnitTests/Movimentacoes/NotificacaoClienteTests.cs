using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Movimentacoes;

public sealed class NotificacaoClienteTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static NotificacaoCliente Rascunho(int andamentos = 1) => NotificacaoCliente.CriarRascunho(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1234567-48.2026.8.26.0001",
        Enumerable.Range(1, andamentos).Select(i => new AndamentoDoAviso(Agora.AddDays(-i), $"Andamento {i}")).ToList(), Agora);

    [Fact]
    public void NaoMarcaEnviadaSemRevisao()
    {
        var aviso = Rascunho();

        aviso.MarcarEnviada(Agora).IsFailure.Should().BeTrue("Enviada exige revisor (o banco também tem CHECK)");
        aviso.Status.Should().Be(StatusNotificacaoCliente.PendenteRevisao);
    }

    [Fact]
    public void RascunhoListaNoMaximo20Andamentos_EAvisaOResto()
    {
        var aviso = Rascunho(25);

        aviso.Mensagem.Should().Contain("Houve 25 novidades").And.Contain("e mais 5 andamento(s)");
        aviso.Mensagem.Split('\n').Count(l => l.StartsWith("- ", StringComparison.Ordinal)).Should().Be(21);
    }

    [Fact]
    public void RascunhoNaoLevaONomeDoCliente()
    {
        Rascunho().Mensagem.Should().StartWith("Prezado(a) cliente,");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MensagemEditadaVazia_ERecusada(string mensagem)
    {
        var resultado = Rascunho().Aprovar(Guid.NewGuid(), mensagem, Agora);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CLIENT_NOTIFICATION_MESSAGE_INVALID));
    }

    [Fact]
    public void Acumular_SoEnquantoPendente()
    {
        var aviso = Rascunho();
        aviso.Descartar(Guid.NewGuid(), Agora);

        aviso.Acumular("1234567-48.2026.8.26.0001", [new AndamentoDoAviso(Agora, "Novo")]).IsFailure.Should().BeTrue();
    }
}
