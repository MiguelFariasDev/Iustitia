using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Events;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Clientes;

public sealed class ClienteAnonimizacaoTests
{
    private static Cliente NovoCliente(TipoCliente tipo = TipoCliente.PessoaFisica)
    {
        var documento = tipo == TipoCliente.PessoaFisica ? "11144477735" : "11222333000181";

        return Cliente.Create(
            Guid.NewGuid(),
            tipo,
            NomeCliente.Create("Maria da Silva").Value,
            Documento.Create(documento, tipo).Value,
            Contato.Create("maria@exemplo.com.br", "85999998888").Value,
            Endereco.Create("Rua A", "10", null, "Centro", "Fortaleza", "CE", "60000000").Value,
            tipo == TipoCliente.PessoaFisica ? new DateOnly(1985, 4, 2) : null,
            "Prefere contato à tarde").Value;
    }

    [Fact]
    public void Anonimizar_RemovesEveryPersonalField_AndDeactivates()
    {
        var cliente = NovoCliente();
        var agora = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        var result = cliente.Anonimizar(agora);

        result.IsSuccess.Should().BeTrue();
        cliente.Nome.Value.Should().Be("Titular anonimizado");
        cliente.NomeBusca.Should().Be("titular anonimizado");
        cliente.Documento.Value.Should().NotBe("11144477735");
        cliente.DocumentoBusca.Should().Be(cliente.Documento.Value);
        cliente.Contato.Email.Should().BeNull();
        cliente.Contato.Telefone.Should().BeNull();
        cliente.Endereco.Logradouro.Should().BeNull();
        cliente.Endereco.Cep.Should().BeNull();
        cliente.DataNascimento.Should().BeNull();
        cliente.Observacoes.Should().BeNull();
        cliente.IsActive.Should().BeFalse();
        cliente.Anonimizado.Should().BeTrue();
        cliente.AnonimizadoEm.Should().Be(agora);
        cliente.DomainEvents.Should().ContainSingle(e => e is ClienteAnonimizadoEvent);
    }

    [Fact]
    public void Anonimizar_Twice_IsRefused()
    {
        var cliente = NovoCliente();
        cliente.Anonimizar(DateTimeOffset.UtcNow);

        var result = cliente.Anonimizar(DateTimeOffset.UtcNow);

        result.Error.Code.Should().Be(ErrorCode.CLIENT_ANONYMIZED.ToString());
    }

    [Fact]
    public void AnAnonymizedClient_CannotBeEditedNorReactivated()
    {
        var cliente = NovoCliente();
        cliente.Anonimizar(DateTimeOffset.UtcNow);

        var update = cliente.Update(NomeCliente.Create("Outro Nome").Value, Contato.Vazio, Endereco.Vazio, null, null);
        var reactivate = cliente.Reactivate();

        update.Error.Code.Should().Be(ErrorCode.CLIENT_ANONYMIZED.ToString());
        reactivate.Error.Code.Should().Be(ErrorCode.CLIENT_ANONYMIZED.ToString());
        cliente.Nome.Value.Should().Be("Titular anonimizado");
    }

    [Theory]
    [InlineData(TipoCliente.PessoaFisica, 11)]
    [InlineData(TipoCliente.PessoaJuridica, 14)]
    public void AnonymousDocument_HasTheRightLength_AndIsNeverAValidDocument(TipoCliente tipo, int length)
    {
        for (var i = 0; i < 200; i++)
        {
            var anonimo = Documento.Anonimo(Guid.NewGuid(), tipo);

            anonimo.Value.Should().HaveLength(length).And.MatchRegex("^[0-9]+$");
            // Nunca colide com o documento real (válido) de outro cliente do escritório.
            Documento.Create(anonimo.Value, tipo).IsFailure.Should().BeTrue();
            anonimo.ToMasked().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void AnonymousDocument_IsStablePerClient_AndDifferentBetweenClients()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Documento.Anonimo(a, TipoCliente.PessoaFisica).Value.Should().Be(Documento.Anonimo(a, TipoCliente.PessoaFisica).Value);
        Documento.Anonimo(a, TipoCliente.PessoaFisica).Value.Should().NotBe(Documento.Anonimo(b, TipoCliente.PessoaFisica).Value);
    }

    [Fact]
    public void LegalEntity_AlsoAnonymizes()
    {
        var cliente = NovoCliente(TipoCliente.PessoaJuridica);

        cliente.Anonimizar(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        cliente.Documento.Value.Should().HaveLength(14);
    }
}
