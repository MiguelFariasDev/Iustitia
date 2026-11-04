using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Events;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Processos;

public class ProcessoTests
{
    private const string CnjValido = "1234567-48.2026.8.26.0001";

    private static Processo Criar() =>
        Processo.Create(
            Guid.NewGuid(),
            CNJNumber.Create(CnjValido).Value,
            Guid.NewGuid(),
            Guid.NewGuid(),
            AreaDireito.Civel).Value;

    [Fact]
    public void Criado_NasceAtivoEGerandoCaptura()
    {
        var processo = Criar();

        processo.Status.Should().Be(StatusProcesso.Ativo);
        processo.GeraCaptura.Should().BeTrue();
        processo.DomainEvents.OfType<ProcessoCriadoEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Criado_PreencheOsDigitosDoCnj()
    {
        // CnjDigits é o que o job envia ao DJEN e o que a busca consulta.
        Criar().CnjDigits.Should().Be("12345674820268260001");
    }

    [Fact]
    public void Criado_SemCliente_Falha()
    {
        var resultado = Processo.Create(
            Guid.NewGuid(), CNJNumber.Create(CnjValido).Value, Guid.Empty, Guid.NewGuid(), AreaDireito.Civel);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CLIENT_REQUIRED));
    }

    [Fact]
    public void Criado_SemResponsavel_Falha()
    {
        var resultado = Processo.Create(
            Guid.NewGuid(), CNJNumber.Create(CnjValido).Value, Guid.NewGuid(), Guid.Empty, AreaDireito.Civel);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_RESPONSIBLE_REQUIRED));
    }

    [Fact]
    public void Criado_ComValorDeCausaNegativo_Falha()
    {
        var resultado = Processo.Create(
            Guid.NewGuid(), CNJNumber.Create(CnjValido).Value, Guid.NewGuid(), Guid.NewGuid(),
            AreaDireito.Civel, valorCausa: -1);

        resultado.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusProcesso.Ativo, true)]
    [InlineData(StatusProcesso.Suspenso, true)]
    [InlineData(StatusProcesso.Encerrado, false)]
    [InlineData(StatusProcesso.Arquivado, false)]
    public void GeraCaptura_SomenteEnquantoOProcessoEAcompanhado(StatusProcesso status, bool esperado)
    {
        // Suspenso continua gerando captura: um processo sobrestado volta a publicar sem
        // aviso, e deixar de consultá-lo é exatamente como se perde um prazo.
        var processo = Criar();

        if (status != StatusProcesso.Ativo)
        {
            processo.ChangeStatus(status).IsSuccess.Should().BeTrue();
        }

        processo.GeraCaptura.Should().Be(esperado);
    }

    [Fact]
    public void ChangeStatus_LevantaEventoComOEstadoAnterior()
    {
        var processo = Criar();
        processo.ClearDomainEvents();

        processo.ChangeStatus(StatusProcesso.Suspenso);

        var evento = processo.DomainEvents.OfType<ProcessoStatusAlteradoEvent>().Should().ContainSingle().Subject;
        evento.StatusAnterior.Should().Be(StatusProcesso.Ativo);
        evento.NovoStatus.Should().Be(StatusProcesso.Suspenso);
    }

    [Fact]
    public void ChangeStatus_ParaOMesmoStatus_Falha()
    {
        var resultado = Criar().ChangeStatus(StatusProcesso.Ativo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_INVALID_STATUS_TRANSITION));
    }

    [Fact]
    public void Arquivado_EEstadoTerminal()
    {
        var processo = Criar();
        processo.Archive().IsSuccess.Should().BeTrue();

        var resultado = processo.ChangeStatus(StatusProcesso.Ativo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_ALREADY_ARCHIVED));
    }

    [Fact]
    public void Update_NaoAlteraONumeroCnj()
    {
        // O número é a identidade do processo e a chave pela qual as publicações já
        // capturadas foram vinculadas — trocá-lo órfãos essas publicações sem deixar rastro.
        var processo = Criar();

        var metodoUpdate = typeof(Processo).GetMethod(nameof(Processo.Update));

        metodoUpdate!.GetParameters().Select(p => p.Name)
            .Should().NotContain(["cnjNumber", "CnjNumber"]);

        processo.CnjNumber.Value.Should().Be(CnjValido);
    }

    [Fact]
    public void EventoDeCriacao_NaoCarregaDadoDoCliente()
    {
        var propriedades = typeof(ProcessoCriadoEvent).GetProperties().Select(p => p.Name).ToList();

        propriedades.Should().Contain("CnjNumber");
        propriedades.Should().NotContain(["ParteContraria", "ValorCausa", "Observacoes"]);
    }
}
