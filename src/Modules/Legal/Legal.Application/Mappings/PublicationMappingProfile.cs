using Advocacia.Modules.Legal.Application.Features.GetPublicationById;
using Advocacia.Modules.Legal.Application.Features.ListPublications;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Mapster;

namespace Advocacia.Modules.Legal.Application.Mappings;

/// <summary>
/// Mapeamentos de <see cref="Publication"/> para os contratos de leitura. Os value objects
/// (CNJNumber, RawContent, PublishedAt) são desempacotados explicitamente — sem isso o
/// Mapster serializaria o objeto inteiro e o RawContent apareceria em toda listagem.
/// </summary>
public sealed class PublicationMappingProfile : IRegister
{
    /// <summary>Tamanho do trecho exibido nas listagens (ver PublicationListItem).</summary>
    public const int ContentPreviewLength = 280;

    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Publication, GetPublicationByIdResponse>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.CnjNumber, src => src.CnjNumber.Value)
            .Map(dest => dest.RawContent, src => src.RawContent.Value)
            .Map(dest => dest.PublishedAt, src => src.PublishedAt.Value)
            .Map(dest => dest.Source, src => src.Source.ToString())
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.Type, src => src.Type == null ? null : src.Type.ToString());

        config.NewConfig<Publication, PublicationListItem>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.CnjNumber, src => src.CnjNumber.Value)
            .Map(dest => dest.PublishedAt, src => src.PublishedAt.Value)
            .Map(dest => dest.Source, src => src.Source.ToString())
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.Type, src => src.Type == null ? null : src.Type.ToString())
            .Map(dest => dest.ContentPreview, src => BuildPreview(src.RawContent.Value));
    }

    /// <summary>Corta o texto em <see cref="ContentPreviewLength"/> caracteres, com elipse quando truncado.</summary>
    public static string BuildPreview(string content) =>
        content.Length <= ContentPreviewLength ? content : content[..ContentPreviewLength] + "…";
}
