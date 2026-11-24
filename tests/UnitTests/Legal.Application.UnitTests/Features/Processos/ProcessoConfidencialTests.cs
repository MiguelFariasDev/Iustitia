using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.Processos;

public sealed class ProcessoConfidencialTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Responsavel = Guid.NewGuid();

    private readonly IProcessoRepository _repository = Substitute.For<IProcessoRepository>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();

    private Processo ProcessoComSigilo(bool confidencial)
    {
        var processo = Processo.Create(
            Tenant, CNJNumber.Create("0000777-07.2018.8.06.0100").Value, Guid.NewGuid(), Responsavel, AreaDireito.Civel).Value;
        processo.DefinirConfidencialidade(confidencial);
        _repository.GetByIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return processo;
    }

    private static ICurrentUser User(Guid? id, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(id);
        user.TenantId.Returns(Tenant);
        user.IsInRole(Arg.Any<string>()).Returns(call => roles.Contains(call.Arg<string>()));

        return user;
    }

    [Theory]
    [InlineData("Lawyer", false)]
    [InlineData("Intern", false)]
    [InlineData("Admin", false)]
    [InlineData("Partner", true)]
    [InlineData("Owner", true)]
    public async Task Get_ConfidentialProcess_IsVisibleOnlyToTheFirmLeadership(string role, bool visible)
    {
        var processo = ProcessoComSigilo(confidencial: true);

        var result = await new GetProcessoByIdHandler(_repository, User(Guid.NewGuid(), role))
            .Handle(new GetProcessoByIdQuery(processo.Id.Value), default);

        result.IsSuccess.Should().Be(visible);
        if (!visible)
        {
            result.Error.Code.Should().Be(ErrorCode.PROCESS_NOT_FOUND.ToString());
        }
    }

    [Fact]
    public async Task Get_ConfidentialProcess_IsVisibleToItsResponsibleLawyer()
    {
        var processo = ProcessoComSigilo(confidencial: true);

        var result = await new GetProcessoByIdHandler(_repository, User(Responsavel, "Lawyer"))
            .Handle(new GetProcessoByIdQuery(processo.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Confidencial.Should().BeTrue();
    }

    [Fact]
    public async Task Get_OrdinaryProcess_IsVisibleToAnyLawyer()
    {
        var processo = ProcessoComSigilo(confidencial: false);

        var result = await new GetProcessoByIdHandler(_repository, User(Guid.NewGuid(), "Lawyer"))
            .Handle(new GetProcessoByIdQuery(processo.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeStatus_OnAConfidentialProcessOfSomeoneElse_DoesNotRevealItNorChangeIt()
    {
        var processo = ProcessoComSigilo(confidencial: true);

        var result = await new ChangeProcessoStatusHandler(_repository, _unitOfWork, User(Guid.NewGuid(), "Lawyer"))
            .Handle(new ChangeProcessoStatusCommand(processo.Id.Value, "Suspenso"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCode.PROCESS_NOT_FOUND.ToString());
        processo.Status.Should().Be(StatusProcesso.Ativo);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetConfidencial_ByAPartner_PersistsTheFlag()
    {
        var processo = ProcessoComSigilo(confidencial: false);

        var result = await new SetProcessoConfidencialHandler(_repository, _unitOfWork, User(Guid.NewGuid(), "Partner"))
            .Handle(new SetProcessoConfidencialCommand(processo.Id.Value, true), default);

        result.IsSuccess.Should().BeTrue();
        processo.Confidencial.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_AsALawyer_AsksTheRepositoryToHideOthersConfidentialProcesses_AsAPartnerDoesNot()
    {
        _repository.ListAsync(default, default, default, default, default, default, default, default, default, default, default, default, default)
            .ReturnsForAnyArgs(Array.Empty<Processo>());
        var lawyerId = Guid.NewGuid();

        await new ListProcessosHandler(_repository, User(lawyerId, "Lawyer")).Handle(new ListProcessosQuery(), default);
        await new ListProcessosHandler(_repository, User(Guid.NewGuid(), "Partner")).Handle(new ListProcessosQuery(), default);

        var calls = _repository.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IProcessoRepository.ListAsync)).ToList();
        calls.Should().HaveCount(2);
        calls[0].GetArguments()[11].Should().Be(lawyerId);
        calls[1].GetArguments()[11].Should().BeNull();
    }
}
