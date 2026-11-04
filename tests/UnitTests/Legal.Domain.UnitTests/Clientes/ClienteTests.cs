using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Events;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Clientes;

public class ClienteTests
{
    private static Cliente Criar(TipoCliente tipo = TipoCliente.PessoaFisica, string documento = "111.444.777-35") =>
        Cliente.Create(
            Guid.NewGuid(),
            tipo,
            NomeCliente.Create("Maria Souza").Value,
            Documento.Create(documento, tipo).Value).Value;

    [Fact]
    public void Criado_NasceAtivo_ELevantaEvento()
    {
        var cliente = Criar();

        cliente.IsActive.Should().BeTrue();
        cliente.DomainEvents.OfType<ClienteCriadoEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Criado_PreencheAsColunasDerivadasDeBusca()
    {
        // NomeBusca e DocumentoBusca existem porque value object convertido não é pesquisável
        // por "contém" no EF Core — se saírem de sincronia, a busca para de achar o cliente.
        var cliente = Criar();

        cliente.NomeBusca.Should().Be("maria souza");
        cliente.DocumentoBusca.Should().Be("11144477735");
    }

    [Fact]
    public void Criado_ComDocumentoDeTipoDivergente_Falha()
    {
        var resultado = Cliente.Create(
            Guid.NewGuid(),
            TipoCliente.PessoaJuridica,
            NomeCliente.Create("Empresa X").Value,
            Documento.Create("111.444.777-35", TipoCliente.PessoaFisica).Value);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.CLIENT_DOCUMENT_INVALID));
    }

    [Fact]
    public void Criado_SemTenant_Falha()
    {
        var resultado = Cliente.Create(
            Guid.Empty,
            TipoCliente.PessoaFisica,
            NomeCliente.Create("Maria").Value,
            Documento.Create("111.444.777-35", TipoCliente.PessoaFisica).Value);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Update_AtualizaNomeEMantemABuscaEmSincronia()
    {
        var cliente = Criar();

        cliente.Update(
            NomeCliente.Create("Maria Souza Lima").Value, Contato.Vazio, Endereco.Vazio,
            dataNascimento: null, observacoes: null)
            .IsSuccess.Should().BeTrue();

        cliente.Nome.Value.Should().Be("Maria Souza Lima");
        cliente.NomeBusca.Should().Be("maria souza lima");
    }

    [Fact]
    public void Desativar_EReativar()
    {
        var cliente = Criar();

        cliente.Deactivate().IsSuccess.Should().BeTrue();
        cliente.IsActive.Should().BeFalse();

        cliente.Reactivate().IsSuccess.Should().BeTrue();
        cliente.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Desativar_DuasVezes_Falha()
    {
        var cliente = Criar();
        cliente.Deactivate();

        var resultado = cliente.Deactivate();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.CLIENT_ALREADY_INACTIVE));
    }

    [Fact]
    public void Reativar_ClienteJaAtivo_Falha()
    {
        var resultado = Criar().Reactivate();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.CLIENT_ALREADY_ACTIVE));
    }

    [Fact]
    public void EventosNuncaCarregamDadoPessoal()
    {
        // O evento é serializado na outbox e propagado por mensageria — documento, e-mail e
        // telefone não podem circular por esses caminhos (claude.md, seção 14).
        var propriedades = typeof(ClienteCriadoEvent).GetProperties().Select(p => p.Name).ToList();

        propriedades.Should().NotContain(["Documento", "Email", "Telefone", "Nome"]);
    }
}
