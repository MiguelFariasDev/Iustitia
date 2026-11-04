using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Clientes;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Clientes;

public class DocumentoTests
{
    // CPFs e CNPJs com dígitos verificadores corretos.
    private const string CpfValido = "111.444.777-35";
    private const string CnpjValido = "11.222.333/0001-81";

    [Theory]
    [InlineData("111.444.777-35")]
    [InlineData("11144477735")]
    [InlineData("111 444 777 35")]
    public void CriaCpf_IgnorandoAPontuacao(string entrada)
    {
        var resultado = Documento.Create(entrada, TipoCliente.PessoaFisica);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Value.Should().Be("11144477735");
    }

    [Fact]
    public void CriaCnpj()
    {
        var resultado = Documento.Create(CnpjValido, TipoCliente.PessoaJuridica);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Value.Should().Be("11222333000181");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("111.444.777-36")]
    public void RejeitaCpfInvalido(string? entrada)
    {
        var resultado = Documento.Create(entrada, TipoCliente.PessoaFisica);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(nameof(ErrorCode.CLIENT_DOCUMENT_INVALID));
    }

    [Theory]
    [InlineData("000.000.000-00")]
    [InlineData("111.111.111-11")]
    public void RejeitaSequenciaDeDigitosIguais(string entrada)
    {
        // Passa na aritmética dos verificadores mas não é documento de ninguém — é o que
        // aparece quando alguém preenche o campo só para o formulário deixar salvar.
        Documento.Create(entrada, TipoCliente.PessoaFisica).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejeitaCpfQuandoOClienteEPessoaJuridica()
    {
        Documento.Create(CpfValido, TipoCliente.PessoaJuridica).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RejeitaCnpjQuandoOClienteEPessoaFisica()
    {
        Documento.Create(CnpjValido, TipoCliente.PessoaFisica).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MascaraCpfParaExibicao()
    {
        Documento.Create(CpfValido, TipoCliente.PessoaFisica).Value.ToMasked().Should().Be("111.444.777-35");
    }

    [Fact]
    public void MascaraCnpjParaExibicao()
    {
        Documento.Create(CnpjValido, TipoCliente.PessoaJuridica).Value.ToMasked().Should().Be("11.222.333/0001-81");
    }

    [Fact]
    public void ToString_NuncaExpoeODocumentoCompleto()
    {
        // CPF é dado pessoal (LGPD) e ToString() é o caminho mais fácil para ele vazar em log.
        var documento = Documento.Create(CpfValido, TipoCliente.PessoaFisica).Value;

        documento.ToString().Should().NotContain("11144477735");
        documento.ToString().Should().NotContain("111.444.777-35");
    }

    [Fact]
    public void Igualdade_EPeloValor()
    {
        var um = Documento.Create(CpfValido, TipoCliente.PessoaFisica).Value;
        var outro = Documento.Create("11144477735", TipoCliente.PessoaFisica).Value;

        um.Should().Be(outro);
    }
}
