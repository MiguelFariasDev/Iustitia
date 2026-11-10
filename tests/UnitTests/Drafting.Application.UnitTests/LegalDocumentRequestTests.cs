using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Drafting.Domain.Pecas;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class LegalDocumentRequestTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static LegalDocumentRequest New() =>
        LegalDocumentRequest.Create(Ids.Tenant, Ids.User, "peticao_inicial", "civel", "Fatos.", "Pedidos.", null, null, Agora).Value;

    [Fact]
    public void NasceNaFilaComProgressoZero()
    {
        var request = New();

        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.Progresso.Should().Be(0);
        request.Tentativas.Should().Be(0);
        request.EtapaAtual.Should().Contain("fila");
        request.Versao.Should().Be(1);
    }

    [Fact]
    public void PedidosVazioEAceito_FatosVazioNao()
    {
        LegalDocumentRequest.Create(Ids.Tenant, Ids.User, "peticao_inicial", "civel", "Fatos.", string.Empty, null, null, Agora)
            .IsSuccess.Should().BeTrue();
        var semFatos = LegalDocumentRequest.Create(Ids.Tenant, Ids.User, "peticao_inicial", "civel", " ", "x", null, null, Agora);

        semFatos.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FIELD_REQUIRED));
    }

    [Fact]
    public void ReservarContaTentativa_EEsperarInfraestruturaDevolve()
    {
        var request = New();

        request.Claim();
        request.Claim();
        request.Tentativas.Should().Be(2);
        request.Status.Should().Be(LegalDocumentStatus.NaFila);

        request.WaitForInfrastructure("Aguardando");
        request.Tentativas.Should().Be(1);
        request.Status.Should().Be(LegalDocumentStatus.Pendente);

        var nova = New();
        nova.WaitForInfrastructure("x");
        nova.Tentativas.Should().Be(0); // nunca fica negativo
    }

    [Fact]
    public void ProgressoFicaEntre0E100()
    {
        var request = New();

        request.SetStage(LegalDocumentStatus.Gerando, "Gerando", 150);
        request.Progresso.Should().Be(100);
        request.SetPartial("t", -5);
        request.Progresso.Should().Be(0);
    }

    [Theory]
    [InlineData(LegalDocumentStatus.Pendente)]
    [InlineData(LegalDocumentStatus.NaFila)]
    [InlineData(LegalDocumentStatus.PreparandoGpu)]
    [InlineData(LegalDocumentStatus.BuscandoContexto)]
    [InlineData(LegalDocumentStatus.Gerando)]
    [InlineData(LegalDocumentStatus.Salvando)]
    public void PodeCancelarEmQualquerEtapaAntesDoFim(LegalDocumentStatus etapa)
    {
        var request = New();
        request.SetStage(etapa, "x", 10);

        var result = request.Cancel(Agora);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(LegalDocumentStatus.Cancelado);
        request.CanceladoEm.Should().Be(Agora);
        request.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void NaoCancelaDepoisDeTerminal()
    {
        var concluida = New();
        concluida.Complete("t", 1, 0m, null, Agora);
        var falhou = New();
        falhou.Fail("e", null, 1, Agora);
        var cancelada = New();
        cancelada.Cancel(Agora);

        foreach (var request in new[] { concluida, falhou, cancelada })
        {
            request.Cancel(Agora).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_CANNOT_CANCEL));
        }

        concluida.Status.Should().Be(LegalDocumentStatus.Concluido);
    }

    [Fact]
    public void Requeue_LimpaOTextoParcial_MasGuardaOMotivo()
    {
        var request = New();
        request.SetPartial("parcial", 50);

        request.Requeue("TimeoutException: prazo");

        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.TextoGerado.Should().BeNull();
        request.Erro.Should().Be("TimeoutException: prazo");
    }

    [Fact]
    public void CompleteLimpaOErroDeUmaTentativaAnterior()
    {
        var request = New();
        request.Requeue("erro antigo");

        request.Complete("Minuta.", 1000, 0.02m, "[\"aviso\"]", Agora);

        request.Erro.Should().BeNull();
        request.Progresso.Should().Be(100);
        request.CustoEstimadoUsd.Should().Be(0.02m);
        request.AvisosJson.Should().Be("[\"aviso\"]");
    }

    [Fact]
    public void ErroETruncadoNoLimiteDaColuna()
    {
        var request = New();

        request.Fail(new string('x', 2000), null, 1, Agora);

        request.Erro!.Length.Should().Be(LegalDocumentRequest.ErroMaxLength);
    }
}
