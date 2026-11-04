namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>
/// Natureza jurídica do cliente. Determina qual documento é exigido (CPF ou CNPJ) e não
/// pode ser alterada depois do cadastro: mudar de PF para PJ é, na prática, outro cliente.
/// </summary>
public enum TipoCliente
{
    PessoaFisica,
    PessoaJuridica,
}
