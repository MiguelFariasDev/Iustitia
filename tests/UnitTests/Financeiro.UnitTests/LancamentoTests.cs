using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using FluentAssertions;

namespace Advocacia.Modules.Financeiro.UnitTests;

public sealed class LancamentoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static Lancamento Novo(decimal valor = 1500.50m, bool jaPago = false, DateOnly? vencimento = null) =>
        Lancamento.Create(
            Tenant, TipoLancamento.Honorario, "  Honorários — ação X  ", " Maria Souza ", "", valor,
            vencimento ?? new DateOnly(2026, 12, 5), jaPago, Agora).Value;

    [Fact]
    public void Create_NormalizesTextAndStartsPending()
    {
        var lancamento = Novo();

        lancamento.Descricao.Should().Be("Honorários — ação X");
        lancamento.Pessoa.Should().Be("Maria Souza");
        lancamento.Processo.Should().BeNull();
        lancamento.Status.Should().Be(StatusLancamento.Pendente);
        lancamento.PagoEm.Should().BeNull();
        lancamento.DomainEvents.Should().ContainSingle(e => e is LancamentoCriadoEvent);
    }

    [Fact]
    public void Create_AlreadyPaid_RecordsThePaymentMoment()
    {
        var lancamento = Novo(jaPago: true);

        lancamento.Status.Should().Be(StatusLancamento.Pago);
        lancamento.PagoEm.Should().Be(Agora);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.123)]
    [InlineData(1_000_000_000_000)]
    public void Create_RejectsInvalidAmounts(double valor)
    {
        var result = Lancamento.Create(
            Tenant, TipoLancamento.ContaPagar, "Aluguel", null, null, (decimal)valor, new DateOnly(2026, 12, 5), false, Agora);

        result.Error.Code.Should().Be(ErrorCode.FINANCE_ENTRY_AMOUNT_INVALID.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankDescription(string descricao)
    {
        var result = Lancamento.Create(
            Tenant, TipoLancamento.ContaPagar, descricao, null, null, 10m, new DateOnly(2026, 12, 5), false, Agora);

        result.Error.Code.Should().Be(ErrorCode.FINANCE_ENTRY_DESCRIPTION_INVALID.ToString());
    }

    [Fact]
    public void Create_RejectsTooLongDescription()
    {
        var result = Lancamento.Create(
            Tenant, TipoLancamento.ContaPagar, new string('x', 201), null, null, 10m, new DateOnly(2026, 12, 5), false, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarComoPago_SetsStatusAndMoment_AndCannotRepeat()
    {
        var lancamento = Novo();

        lancamento.MarcarComoPago(Agora).IsSuccess.Should().BeTrue();
        lancamento.Status.Should().Be(StatusLancamento.Pago);
        lancamento.PagoEm.Should().Be(Agora);
        lancamento.DomainEvents.Should().Contain(e => e is LancamentoPagoEvent);

        lancamento.MarcarComoPago(Agora.AddDays(1)).Error.Code.Should().Be(ErrorCode.FINANCE_ENTRY_ALREADY_PAID.ToString());
        lancamento.PagoEm.Should().Be(Agora);
    }

    [Fact]
    public void EstaAtrasado_OnlyWhenPendingAndPastDue()
    {
        var vencido = Novo(vencimento: new DateOnly(2026, 11, 20));
        var hoje = new DateOnly(2026, 11, 24);

        vencido.EstaAtrasado(hoje).Should().BeTrue();
        Novo(vencimento: hoje).EstaAtrasado(hoje).Should().BeFalse("vence hoje ainda não é atraso");
        Novo(vencimento: new DateOnly(2026, 11, 20), jaPago: true).EstaAtrasado(hoje).Should().BeFalse("o que foi pago nunca atrasa");
    }
}
