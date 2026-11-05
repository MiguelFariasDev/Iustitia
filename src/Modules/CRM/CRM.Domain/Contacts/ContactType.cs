namespace Advocacia.Modules.CRM.Domain.Contacts;

/// <summary>
/// Papel da pessoa em relação ao escritório. Contato NÃO é cliente: quem o escritório
/// representa vive em Legal (Cliente); aqui ficam as pessoas com quem ele precisa falar.
/// </summary>
public enum ContactType
{
    ParteContraria = 1,
    Testemunha = 2,
    Fornecedor = 3,
    Outros = 4,
}
