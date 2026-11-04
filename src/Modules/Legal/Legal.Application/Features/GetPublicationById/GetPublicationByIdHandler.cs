using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;
using MapsterMapper;

namespace Advocacia.Modules.Legal.Application.Features.GetPublicationById;

/// <summary>
/// Não precisa filtrar por tenant explicitamente: o filtro global do LegalDbContext e o RLS
/// do Postgres já restringem a consulta ao escritório da requisição, então uma publicação
/// de outro tenant simplesmente não é encontrada (404, não 403 — não revelamos a existência
/// de dados de outro escritório).
/// </summary>
public sealed class GetPublicationByIdHandler(
    IPublicationRepository publicationRepository, IClienteRepository clienteRepository, IMapper mapper)
    : IQueryHandler<GetPublicationByIdQuery, GetPublicationByIdResponse>
{
    public async Task<Result<GetPublicationByIdResponse>> Handle(
        GetPublicationByIdQuery request, CancellationToken cancellationToken)
    {
        var publication = await publicationRepository.GetByIdAsync(
            PublicationId.From(request.PublicationId), cancellationToken);

        if (publication is null)
        {
            return Result.Failure<GetPublicationByIdResponse>(ErrorFactory.From(ErrorCode.PUBLICATION_NOT_FOUND));
        }

        string? clienteNome = null;
        if (publication.ClienteId is { } clienteId)
        {
            var nomes = await clienteRepository.GetNomesAsync(publication.TenantId, [clienteId], cancellationToken);
            clienteNome = nomes.GetValueOrDefault(clienteId);
        }

        return Result.Success(mapper.Map<GetPublicationByIdResponse>(publication) with { ClienteNome = clienteNome });
    }
}
