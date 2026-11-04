using Advocacia.Modules.Legal.Domain.Clientes;

namespace Advocacia.Modules.Legal.Application.Features.Clientes;

/// <summary>
/// Mapeamento manual, não Mapster: os value objects precisam ser desempacotados com regra
/// (documento sai MASCARADO, telefone também) e um mapeamento por convenção erraria isso em
/// silêncio — serializando o objeto inteiro ou o valor cru.
/// </summary>
internal static class ClienteMapping
{
    internal static ClienteResponse ToResponse(this Cliente cliente) => new(
        cliente.Id.Value,
        cliente.Tipo.ToString(),
        cliente.Nome.Value,
        cliente.Documento.ToMasked(),
        cliente.Contato.Email,
        cliente.Contato.TelefoneMascarado(),
        cliente.Endereco.Logradouro,
        cliente.Endereco.Numero,
        cliente.Endereco.Complemento,
        cliente.Endereco.Bairro,
        cliente.Endereco.Cidade,
        cliente.Endereco.Uf,
        cliente.Endereco.Cep,
        cliente.DataNascimento,
        cliente.Observacoes,
        cliente.IsActive,
        cliente.CreatedAt);

    internal static ClienteListItem ToListItem(this Cliente cliente) => new(
        cliente.Id.Value,
        cliente.Tipo.ToString(),
        cliente.Nome.Value,
        cliente.Documento.ToMasked(),
        cliente.Contato.Email,
        cliente.Contato.TelefoneMascarado(),
        cliente.IsActive);
}
