using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.Processos;

public sealed class ClienteLgpdTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public ClienteLgpdTests()
    {
        _user.TenantId.Returns(Tenant);
        _clock.UtcNow.Returns(Agora);
    }

    private Cliente ClienteExistente()
    {
        var cliente = Cliente.Create(
            Tenant, TipoCliente.PessoaFisica, NomeCliente.Create("Maria da Silva").Value,
            Documento.Create("11144477735", TipoCliente.PessoaFisica).Value,
            Contato.Create("maria@exemplo.com.br", "85999998888").Value).Value;
        _clientes.GetByIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        return cliente;
    }

    private static Processo ProcessoDe(Cliente cliente, string cnj, bool encerrado)
    {
        var processo = Processo.Create(
            Tenant, CNJNumber.Create(cnj).Value, cliente.Id.Value, Guid.NewGuid(), AreaDireito.Civel,
            assunto: "Indenização", parteContraria: "Empresa X").Value;
        if (encerrado)
        {
            processo.ChangeStatus(StatusProcesso.Arquivado);
        }

        return processo;
    }

    private AnonimizarClienteHandler Anonimizador() => new(_clientes, _processos, _unitOfWork, _user, _clock);

    [Fact]
    public async Task Anonymize_WithAnActiveProcess_IsRefusedAndChangesNothing()
    {
        var cliente = ClienteExistente();
        _processos.ListDoClienteAsync(Tenant, cliente.Id.Value, Arg.Any<CancellationToken>())
            .Returns([ProcessoDe(cliente, "0000777-07.2018.8.06.0100", encerrado: false)]);

        var result = await Anonimizador().Handle(new AnonimizarClienteCommand(cliente.Id.Value), default);

        result.Error.Code.Should().Be(ErrorCode.CLIENT_HAS_ACTIVE_PROCESSES.ToString());
        cliente.Anonimizado.Should().BeFalse();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Anonymize_WhenAllProcessesAreArchived_Succeeds()
    {
        var cliente = ClienteExistente();
        _processos.ListDoClienteAsync(Tenant, cliente.Id.Value, Arg.Any<CancellationToken>())
            .Returns([ProcessoDe(cliente, "0000777-07.2018.8.06.0100", encerrado: true)]);

        var result = await Anonimizador().Handle(new AnonimizarClienteCommand(cliente.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        cliente.Anonimizado.Should().BeTrue();
        cliente.AnonimizadoEm.Should().Be(Agora);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Anonymize_UnknownClient_IsNotFound()
    {
        var result = await Anonimizador().Handle(new AnonimizarClienteCommand(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(ErrorCode.CLIENT_NOT_FOUND.ToString());
    }

    [Fact]
    public async Task Export_ReturnsTheCompleteDataOfTheSubject_AndTheirProcesses()
    {
        var cliente = ClienteExistente();
        _processos.ListDoClienteAsync(Tenant, cliente.Id.Value, Arg.Any<CancellationToken>())
            .Returns([ProcessoDe(cliente, "0000777-07.2018.8.06.0100", encerrado: false)]);

        var result = await new ExportarDadosClienteHandler(_clientes, _processos, _user, _clock)
            .Handle(new ExportarDadosClienteQuery(cliente.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.GeradoEm.Should().Be(Agora);
        result.Value.Titular.Nome.Should().Be("Maria da Silva");
        // Cópia ao titular: documento e contato COMPLETOS, não mascarados.
        result.Value.Titular.Documento.Should().Be("11144477735");
        result.Value.Titular.Email.Should().Be("maria@exemplo.com.br");
        result.Value.Titular.Telefone.Should().Be("85999998888");
        result.Value.Processos.Should().ContainSingle().Which.ParteContraria.Should().Be("Empresa X");
    }
}
