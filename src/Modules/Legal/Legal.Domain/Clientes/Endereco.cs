using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>
/// Endereço do cliente — inteiramente opcional, como o <see cref="Contato"/>. Persistido
/// como colunas separadas (e não JSON) porque endereço é dado que se consulta e se filtra:
/// "clientes da comarca de X" é uma pergunta real de escritório.
/// </summary>
public sealed class Endereco : ValueObject
{
    public static readonly Endereco Vazio = new(null, null, null, null, null, null, null);

    public string? Logradouro { get; }

    public string? Numero { get; }

    public string? Complemento { get; }

    public string? Bairro { get; }

    public string? Cidade { get; }

    /// <summary>Sigla da UF, em maiúsculas.</summary>
    public string? Uf { get; }

    /// <summary>Somente dígitos (8).</summary>
    public string? Cep { get; }

    private Endereco(string? logradouro, string? numero, string? complemento, string? bairro, string? cidade, string? uf, string? cep)
    {
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
    }

    /// <summary>
    /// Nunca falha: endereço é opcional e parcialmente preenchido é o normal. Campos em
    /// branco viram nulo; CEP e UF são normalizados. Recusar um cadastro por causa de um
    /// endereço incompleto atrapalharia exatamente o caso comum.
    /// </summary>
    public static Result<Endereco> Create(
        string? logradouro, string? numero, string? complemento, string? bairro, string? cidade, string? uf, string? cep)
    {
        var cepDigits = string.IsNullOrWhiteSpace(cep) ? null : new string(cep.Where(char.IsDigit).ToArray());

        return Result.Success(new Endereco(
            Normalizar(logradouro),
            Normalizar(numero),
            Normalizar(complemento),
            Normalizar(bairro),
            Normalizar(cidade),
            Normalizar(uf)?.ToUpperInvariant(),
            string.IsNullOrEmpty(cepDigits) ? null : cepDigits));
    }

    public static Endereco FromPersistence(
        string? logradouro, string? numero, string? complemento, string? bairro, string? cidade, string? uf, string? cep) =>
        new(logradouro, numero, complemento, bairro, cidade, uf, cep);

    public bool IsEmpty => Logradouro is null && Numero is null && Complemento is null
        && Bairro is null && Cidade is null && Uf is null && Cep is null;

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Logradouro;
        yield return Numero;
        yield return Complemento;
        yield return Bairro;
        yield return Cidade;
        yield return Uf;
        yield return Cep;
    }
}
