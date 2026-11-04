using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.ConsultarProcessoTribunal;

public class ConsultarProcessoTribunalHandlerTests
{
    private const string CnjValido = "0000832-35.2018.4.01.3202";

    private readonly IConsultaProcessoTribunal _consulta = Substitute.For<IConsultaProcessoTribunal>();

    private ConsultarProcessoTribunalHandler CreateHandler() => new(_consulta);

    private static ProcessoTribunal Processo() => new(
        CnjValido, "TRF1", "JE", new DateOnly(2018, 10, 29), EmSegredoDeJustica: false,
        "Procedimento do Juizado Especial Cível", "Pje", "JEF Adj - Tefé",
        ["Concessão"], [new MovimentoProcesso("Sentença", DateTimeOffset.UtcNow)]);

    [Fact]
    public async Task Handle_QuandoEncontrado_DevolveOsMetadados()
    {
        _consulta.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ProcessoTribunal?>(Processo()));

        var result = await CreateHandler().Handle(new ConsultarProcessoTribunalQuery(CnjValido), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Found.Should().BeTrue();
        result.Value.Tribunal.Should().Be("TRF1");
        result.Value.Processo!.Classe.Should().Be("Procedimento do Juizado Especial Cível");
    }

    /// <summary>
    /// Não encontrado é SUCESSO com Found=false, não erro: a tela precisa distinguir "o
    /// tribunal não conhece este processo" (preencha manualmente) de "a consulta falhou"
    /// (tente de novo). Devolver erro nos dois casos apagaria essa diferença.
    /// </summary>
    [Fact]
    public async Task Handle_QuandoNaoEncontrado_DevolveSucessoComFoundFalso()
    {
        _consulta.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ProcessoTribunal?>(null));

        var result = await CreateHandler().Handle(new ConsultarProcessoTribunalQuery(CnjValido), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Found.Should().BeFalse();
        result.Value.Processo.Should().BeNull();
        // O tribunal segue preenchido: a tela mostra "não encontrado no TRF1", não "não encontrado".
        result.Value.Tribunal.Should().Be("TRF1");
    }

    [Fact]
    public async Task Handle_QuandoAConsultaFalha_PropagaOErro()
    {
        var erro = ErrorFactory.From(ErrorCode.CNJ_RATE_LIMITED);
        _consulta.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ProcessoTribunal?>(erro));

        var result = await CreateHandler().Handle(new ConsultarProcessoTribunalQuery(CnjValido), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_RATE_LIMITED));
    }

    [Fact]
    public async Task Handle_ComNumeroInvalido_NaoChamaAConsulta()
    {
        // Dígito verificador trocado: 35 -> 36.
        var result = await CreateHandler().Handle(
            new ConsultarProcessoTribunalQuery("0000832-36.2018.4.01.3202"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_CNJ_INVALID));
        await _consulta.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, cancellationToken: default);
    }

    [Fact]
    public void Validator_RejeitaNumeroComDigitoVerificadorErrado()
    {
        var resultado = new ConsultarProcessoTribunalValidator()
            .Validate(new ConsultarProcessoTribunalQuery("0000832-36.2018.4.01.3202"));

        resultado.IsValid.Should().BeFalse();
    }
}
