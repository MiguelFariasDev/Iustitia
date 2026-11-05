using Advocacia.Modules.CRM.Domain.Contacts;

namespace Advocacia.Modules.CRM.Application.Features.Contacts;

/// <summary>Mapeamento manual (não Mapster): documento e telefone precisam sair mascarados.</summary>
internal static class ContactMapping
{
    internal static ContactResponse ToResponse(this Contact contact) => new(
        contact.Id.Value,
        contact.Type.ToString(),
        contact.Name,
        Contact.MaskDocument(contact.Document),
        contact.Email,
        Contact.MaskPhone(contact.Phone),
        contact.Notes,
        contact.IsActive,
        contact.CreatedAt);
}
