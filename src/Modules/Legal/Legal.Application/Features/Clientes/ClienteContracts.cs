using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.Clientes;

/// <summary>Dados de contato e endereço — opcionais em bloco, como no domínio.</summary>
public sealed record ContatoRequest(string? Email, string? Telefone);

public sealed record EnderecoRequest(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep);

/// <summary>
/// Cadastro de cliente. <c>Tipo</c> é "PessoaFisica" ou "PessoaJuridica" e define se o
/// documento esperado é CPF ou CNPJ.
/// </summary>
public sealed record CreateClienteCommand(
    string Tipo,
    string Nome,
    string Documento,
    ContatoRequest? Contato = null,
    EnderecoRequest? Endereco = null,
    DateOnly? DataNascimento = null,
    string? Observacoes = null) : ICommand<ClienteResponse>;

/// <summary>Tipo e documento NÃO entram: são a identidade do cliente e não são alteráveis (ver Cliente.Update).</summary>
public sealed record UpdateClienteCommand(
    Guid ClienteId,
    string Nome,
    ContatoRequest? Contato = null,
    EnderecoRequest? Endereco = null,
    DateOnly? DataNascimento = null,
    string? Observacoes = null) : ICommand;

public sealed record DeactivateClienteCommand(Guid ClienteId) : ICommand;

public sealed record ReactivateClienteCommand(Guid ClienteId) : ICommand;

public sealed record GetClienteByIdQuery(Guid ClienteId) : IQuery<ClienteResponse>;

public sealed record ListClientesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Tipo = null,
    bool? ApenasAtivos = null,
    string? Busca = null) : IQuery<ListClientesResponse>;

/// <summary>
/// Detalhe do cliente. O documento vem MASCARADO para exibição
/// (000.000.000-00 / 00.000.000/0000-00) — o valor cru não sai da API.
/// </summary>
public sealed record ClienteResponse(
    Guid Id,
    string Tipo,
    string Nome,
    string Documento,
    string? Email,
    string? Telefone,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep,
    DateOnly? DataNascimento,
    string? Observacoes,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Item de listagem — sem endereço nem observações, que só interessam no detalhe.</summary>
public sealed record ClienteListItem(
    Guid Id,
    string Tipo,
    string Nome,
    string Documento,
    string? Email,
    string? Telefone,
    bool IsActive);

public sealed record ListClientesResponse(
    IReadOnlyList<ClienteListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
