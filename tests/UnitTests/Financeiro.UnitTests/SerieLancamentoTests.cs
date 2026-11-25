using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Financeiro.Application.Abstractions;
using Advocacia.Modules.Financeiro.Application.Features.Lancamentos;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Financeiro.UnitTests;

/// <summary>Parcelamento (total dividido) e cobrança mensal (valor repetido).</summary>
public sealed class SerieLancamentoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<Lancamento> Serie(
        RecorrenciaLancamento recorrencia, int quantidade, decimal valor, DateOnly primeiro, bool primeiraPaga = false)
    {
        var resultado = Lancamento.CriarSerie(
            Tenant, TipoLancamento.Honorario, recorrencia, quantidade, "Honorários", "Cliente X", null, valor, primeiro, primeiraPaga, Agora);
        resultado.IsSuccess.Should().BeTrue();

        return resultado.Value;
    }

    [Fact]
    public void Parcelado_DivideOTotal_ECentavosQueSobramVaoParaAUltima()
    {
        var parcelas = Serie(RecorrenciaLancamento.Parcelado, 3, 1000m, new DateOnly(2026, 12, 10));

        parcelas.Select(p => p.Valor).Should().Equal(333.33m, 333.33m, 333.34m);
        parcelas.Sum(p => p.Valor).Should().Be(1000m);
        parcelas.Select(p => p.Parcela).Should().Equal(1, 2, 3);
        parcelas.Should().OnlyContain(p => p.TotalParcelas == 3 && p.Recorrencia == RecorrenciaLancamento.Parcelado);
        parcelas.Select(p => p.SerieId).Distinct().Should().ContainSingle().Which.Should().NotBeNull();
    }

    [Fact]
    public void Mensal_RepeteOValor_EVenceNoMesmoDiaDosMesesSeguintes()
    {
        var meses = Serie(RecorrenciaLancamento.Mensal, 3, 1500m, new DateOnly(2026, 12, 5));

        meses.Should().OnlyContain(m => m.Valor == 1500m && m.Recorrencia == RecorrenciaLancamento.Mensal);
        meses.Select(m => m.Vencimento).Should().Equal(
            new DateOnly(2026, 12, 5), new DateOnly(2027, 1, 5), new DateOnly(2027, 2, 5));
    }

    [Fact]
    public void DiaQueOMesNaoTem_CaiNoUltimoDia_SemArrastarOsSeguintes()
    {
        var meses = Serie(RecorrenciaLancamento.Mensal, 3, 100m, new DateOnly(2027, 1, 31));

        meses.Select(m => m.Vencimento).Should().Equal(
            new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31));
    }

    [Fact]
    public void PrimeiraPaga_MarcaSoAEntrada()
    {
        var parcelas = Serie(RecorrenciaLancamento.Parcelado, 4, 400m, new DateOnly(2026, 11, 24), primeiraPaga: true);

        parcelas[0].Status.Should().Be(StatusLancamento.Pago);
        parcelas.Skip(1).Should().OnlyContain(p => p.Status == StatusLancamento.Pendente && p.PagoEm == null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(61)]
    public void QuantidadeForaDoLimite_EhRecusada(int quantidade)
    {
        var resultado = Lancamento.CriarSerie(
            Tenant, TipoLancamento.Honorario, RecorrenciaLancamento.Mensal, quantidade, "X", null, null, 10m,
            new DateOnly(2026, 12, 1), false, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.FINANCE_ENTRY_INSTALLMENTS_INVALID));
    }

    [Fact]
    public void ParceladoComValorMenorQueUmCentavoPorParcela_EhRecusado()
    {
        var resultado = Lancamento.CriarSerie(
            Tenant, TipoLancamento.Honorario, RecorrenciaLancamento.Parcelado, 10, "X", null, null, 0.05m,
            new DateOnly(2026, 12, 1), false, Agora);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_GravaTodasAsParcelas_EDevolveAPrimeira()
    {
        var repository = Substitute.For<ILancamentoRepository>();
        var unitOfWork = Substitute.For<IFinanceiroUnitOfWork>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IDateTimeProvider>();
        user.TenantId.Returns(Tenant);
        clock.UtcNow.Returns(Agora);
        var handler = new CreateLancamentoHandler(repository, unitOfWork, user, clock);

        var resposta = await handler.Handle(
            new CreateLancamentoCommand("Honorario", "Contrato", "Cliente", null, 1200m, new DateOnly(2026, 12, 10),
                Recorrencia: "Parcelado", Parcelas: 12), default);

        resposta.IsSuccess.Should().BeTrue();
        resposta.Value.Parcela.Should().Be(1);
        resposta.Value.TotalParcelas.Should().Be(12);
        resposta.Value.Valor.Should().Be(100m);
        repository.Received(12).Add(Arg.Any<Lancamento>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validador_ExigeParcelasSoNasSeries()
    {
        var validador = new CreateLancamentoValidator();
        var baseCmd = new CreateLancamentoCommand("Honorario", "X", null, null, 10m, new DateOnly(2026, 12, 1));

        validador.Validate(baseCmd).IsValid.Should().BeTrue();
        validador.Validate(baseCmd with { Recorrencia = "Mensal", Parcelas = 1 }).IsValid.Should().BeFalse();
        validador.Validate(baseCmd with { Recorrencia = "Mensal", Parcelas = 12 }).IsValid.Should().BeTrue();
        validador.Validate(baseCmd with { Recorrencia = "Semanal", Parcelas = 4 }).IsValid.Should().BeFalse();
    }
}
