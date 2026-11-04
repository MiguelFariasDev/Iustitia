using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.GetPublicationById;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.GetPublicationById;

public class GetPublicationByIdHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly GetPublicationByIdHandler _handler;

    public GetPublicationByIdHandlerTests()
    {
        _handler = new GetPublicationByIdHandler(_repository, Substitute.For<IClienteRepository>(), TestMapper.Create());
    }

    [Fact]
    public async Task Handle_WithExistingPublication_ReturnsTheFullDetail()
    {
        var publication = TestPublications.Create(_tenantId, content: "Intimação para manifestação.");
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(new GetPublicationByIdQuery(publication.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var response = result.Value;
        response.Id.Should().Be(publication.Id.Value);
        response.CnjNumber.Should().Be(TestPublications.ValidCnjNumber);
        response.Source.Should().Be(nameof(PublicationSource.DJEN));
        response.Status.Should().Be(nameof(PublicationStatus.PendingReview));
        response.Type.Should().BeNull();
        response.IsRead.Should().BeFalse();

        // A consulta por id é a tela de leitura: aqui o inteiro teor DEVE vir.
        response.RawContent.Should().Be("Intimação para manifestação.");
    }

    [Fact]
    public async Task Handle_MapsTheActTypeWhenPresent()
    {
        var publication = TestPublications.Create(_tenantId);
        publication.SetType(PublicationType.Sentenca);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(new GetPublicationByIdQuery(publication.Id.Value), CancellationToken.None);

        result.Value.Type.Should().Be(nameof(PublicationType.Sentenca));
    }

    [Fact]
    public async Task Handle_WithUnknownId_ReturnsNotFound()
    {
        // Publicação de outro tenant cai aqui também: o filtro global e o RLS a tornam
        // invisível, e responder 404 (não 403) evita confirmar que ela existe.
        _repository.GetByIdAsync(Arg.Any<PublicationId>(), Arg.Any<CancellationToken>()).Returns((Publication?)null);

        var result = await _handler.Handle(new GetPublicationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_NOT_FOUND));
    }
}
