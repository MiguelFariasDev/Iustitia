using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Financeiro.Application.Abstractions;
using Advocacia.Modules.Financeiro.Application.Features.Lancamentos;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Financeiro.UnitTests;

public sealed class LancamentoHandlersTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IFinanceiroUnitOfWork _unitOfWork = Substitute.For<IFinanceiroUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public LancamentoHandlersTests()
    {
        _user.TenantId.Returns(Tenant);
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Create_PersistsInTheCallersTenant_AndReportsStatus()
    {
        var handler = new CreateLancamentoHandler(_repository, _unitOfWork, _user, _clock);

        var result = await handler.Handle(
            new CreateLancamentoCommand("ContaPagar", "Aluguel", "Imobiliária", null, 4200m, new DateOnly(2026, 11, 20)), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Atrasado", "venceu em 20/11 e hoje é 24/11");
        _repository.Received(1).Add(Arg.Is<Lancamento>(l => l.TenantId == Tenant && l.Valor == 4200m));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithoutATenant_IsRefused()
    {
        _user.TenantId.Returns((Guid?)null);

        var result = await new CreateLancamentoHandler(_repository, _unitOfWork, _user, _clock).Handle(
            new CreateLancamentoCommand("Honorario", "X", null, null, 10m, new DateOnly(2026, 12, 1)), default);

        result.Error.Code.Should().Be(ErrorCode.AUTHORIZATION_TENANT_MISMATCH.ToString());
    }

    [Fact]
    public async Task Pay_UnknownEntry_IsNotFound()
    {
        var result = await new MarcarLancamentoPagoHandler(_repository, _unitOfWork, _clock)
            .Handle(new MarcarLancamentoPagoCommand(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(ErrorCode.FINANCE_ENTRY_NOT_FOUND.ToString());
    }

    [Fact]
    public async Task Pay_Twice_ReportsConflict()
    {
        var lancamento = Lancamento.Create(
            Tenant, TipoLancamento.Honorario, "X", null, null, 10m, new DateOnly(2026, 12, 1), true, _clock.UtcNow).Value;
        _repository.GetByIdAsync(lancamento.Id, Arg.Any<CancellationToken>()).Returns(lancamento);

        var result = await new MarcarLancamentoPagoHandler(_repository, _unitOfWork, _clock)
            .Handle(new MarcarLancamentoPagoCommand(lancamento.Id.Value), default);

        result.Error.Code.Should().Be(ErrorCode.FINANCE_ENTRY_ALREADY_PAID.ToString());
    }

    [Fact]
    public async Task Summary_BalanceIsReceivedMinusPaid()
    {
        _repository.TotaisAsync(Tenant, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TotaisFinanceiros(100m, 200m, 300m, 400m, Recebido: 1000m, Pago: 250.50m));

        var result = await new GetResumoFinanceiroHandler(_repository, _user, _clock).Handle(new GetResumoFinanceiroQuery(), default);

        result.Value.Saldo.Should().Be(749.50m);
        result.Value.FaturamentoMes.Should().Be(100m);
    }

    [Fact]
    public async Task Summary_MonthBoundariesFollowBrasiliaTime_NotUtc()
    {
        // 01/12 01:30 UTC ainda é 30/11 22:30 em Brasília: o "mês corrente" é novembro.
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 12, 1, 1, 30, 0, TimeSpan.Zero));
        DateTimeOffset? inicio = null;
        DateTimeOffset? fim = null;
        _repository.TotaisAsync(Tenant, Arg.Do<DateTimeOffset>(d => inicio = d), Arg.Do<DateTimeOffset>(d => fim = d), Arg.Any<CancellationToken>())
            .Returns(new TotaisFinanceiros(0, 0, 0, 0, 0, 0));

        await new GetResumoFinanceiroHandler(_repository, _user, _clock).Handle(new GetResumoFinanceiroQuery(), default);

        inicio!.Value.UtcDateTime.Should().Be(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
        fim!.Value.UtcDateTime.Should().Be(new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CashFlow_ReturnsOneItemPerMonth_WithZerosAndGroupsInBrasiliaTime()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        _repository.PagamentosAsync(Tenant, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            new PagamentoRealizado(false, 1000m, new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero)),
            new PagamentoRealizado(true, 400m, new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero)),
            // 01/10 02:00 UTC = 30/09 23:00 em Brasília: pertence a SETEMBRO.
            new PagamentoRealizado(false, 70m, new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero)),
        ]);

        var result = await new GetFluxoCaixaHandler(_repository, _user, _clock).Handle(new GetFluxoCaixaQuery(6), default);

        result.Value.Should().HaveCount(6);
        result.Value[^1].Should().Be(new FluxoCaixaMesItem(2026, 11, 1000m, 400m));
        result.Value[^2].Should().Be(new FluxoCaixaMesItem(2026, 10, 0m, 0m));
        result.Value[^3].Should().Be(new FluxoCaixaMesItem(2026, 9, 70m, 0m));
        result.Value[0].Should().Be(new FluxoCaixaMesItem(2026, 6, 0m, 0m));
    }
}
