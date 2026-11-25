using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Jobs;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Jobs;

public sealed class AcompanhamentoMovimentacoesJobTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<Guid> _tenantsDosEscopos = [];

    private AcompanhamentoMovimentacoesJob Job(bool ligado)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ =>
        {
            var contexto = Substitute.For<ITenantContext>();
            contexto.When(c => c.SetTenant(Arg.Any<Guid>())).Do(c => _tenantsDosEscopos.Add(c.Arg<Guid>()));
            return contexto;
        });
        services.AddScoped(_ => _sender);
        var provider = services.BuildServiceProvider();

        return new AcompanhamentoMovimentacoesJob(
            _processos,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ITenantContext>(),
            Options.Create(new AcompanhamentoMovimentacoesJobOptions { Enabled = ligado, PausaEntreProcessosMs = 0 }),
            NullLogger<AcompanhamentoMovimentacoesJob>.Instance);
    }

    private static Processo Processo(string cnj) =>
        Domain.Processos.Processo.Create(Tenant, CNJNumber.Create(cnj).Value, Guid.NewGuid(), Guid.NewGuid(), AreaDireito.Civel).Value;

    [Fact]
    public async Task Desligado_NaoConsultaNada()
    {
        await Job(ligado: false).ExecuteAsync(CancellationToken.None);

        await _processos.DidNotReceiveWithAnyArgs().ListTenantsComProcessosAtivosAsync(default);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(SincronizarMovimentacoesCommand)!, default);
    }

    [Fact]
    public async Task Ligado_SincronizaCadaProcesso_NumEscopoProprio_ESegueDepoisDeUmaFalha()
    {
        var primeiro = Processo("1234567-48.2026.8.26.0001");
        var segundo = Processo("0000777-07.2018.8.06.0100");
        var terceiro = Processo("0000832-35.2018.4.01.3202");
        _processos.ListTenantsComProcessosAtivosAsync(Arg.Any<CancellationToken>()).Returns([Tenant]);
        _processos.ListParaCapturaAsync(Tenant, Arg.Any<CancellationToken>()).Returns([primeiro, segundo, terceiro]);
        _sender.Send(Arg.Is<SincronizarMovimentacoesCommand>(c => c.ProcessoId == primeiro.Id.Value), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("falha de gravação"));
        _sender.Send(Arg.Is<SincronizarMovimentacoesCommand>(c => c.ProcessoId == segundo.Id.Value), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SincronizarMovimentacoesResponse>(ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, "TJCE")));
        _sender.Send(Arg.Is<SincronizarMovimentacoesCommand>(c => c.ProcessoId == terceiro.Id.Value), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SincronizarMovimentacoesResponse(true, "datajud", false, 2, true, true)));

        await Job(ligado: true).ExecuteAsync(CancellationToken.None);

        await _sender.Received(3).Send(Arg.Is<SincronizarMovimentacoesCommand>(c => c.OrigemJob), Arg.Any<CancellationToken>());
        _tenantsDosEscopos.Should().Equal(Tenant, Tenant, Tenant);
    }
}
