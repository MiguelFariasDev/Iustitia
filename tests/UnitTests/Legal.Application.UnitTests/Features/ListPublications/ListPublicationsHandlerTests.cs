using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.ListPublications;
using Advocacia.Modules.Legal.Application.Mappings;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.ListPublications;

public class ListPublicationsHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ListPublicationsHandler _handler;

    public ListPublicationsHandlerTests()
    {
        _currentUser.TenantId.Returns(_tenantId);
        _repository
            .SearchAsync(_tenantId, Arg.Any<PublicationSearchCriteria>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _clientes
            .GetNomesAsync(_tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _handler = new ListPublicationsHandler(_repository, _clientes, _currentUser, TestMapper.Create());
    }

    [Fact]
    public async Task Handle_WithoutTenantInContext_Fails()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await _handler.Handle(new ListPublicationsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task Handle_ReturnsPagedItems()
    {
        _repository
            .SearchAsync(_tenantId, Arg.Any<PublicationSearchCriteria>(), 0, 20, Arg.Any<CancellationToken>())
            .Returns([TestPublications.Create(_tenantId, externalId: "cnj-1"), TestPublications.Create(_tenantId, externalId: "cnj-2")]);
        _repository.CountSearchAsync(_tenantId, Arg.Any<PublicationSearchCriteria>(), Arg.Any<CancellationToken>()).Returns(2);

        var result = await _handler.Handle(new ListPublicationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        result.Value.TotalCount.Should().Be(2);
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TruncatesTheContentIntoAPreview()
    {
        // O item de listagem nunca carrega o inteiro teor: 20 publicações completas seriam
        // centenas de KB de dado sigiloso trafegando sem necessidade.
        var longContent = new string('a', PublicationMappingProfile.ContentPreviewLength + 500);
        _repository
            .SearchAsync(_tenantId, Arg.Any<PublicationSearchCriteria>(), 0, 20, Arg.Any<CancellationToken>())
            .Returns([TestPublications.Create(_tenantId, content: longContent)]);

        var result = await _handler.Handle(new ListPublicationsQuery(), CancellationToken.None);

        var preview = result.Value.Items.Single().ContentPreview;
        preview.Should().HaveLength(PublicationMappingProfile.ContentPreviewLength + 1);
        preview.Should().EndWith("…");
    }

    [Fact]
    public async Task Handle_FillsTheClienteNameWithASingleLookupForThePage()
    {
        var clienteId = Guid.NewGuid();
        var publication = TestPublications.Create(_tenantId);
        publication.LinkTo(Guid.NewGuid(), clienteId, Guid.NewGuid());
        _repository
            .SearchAsync(_tenantId, Arg.Any<PublicationSearchCriteria>(), 0, 20, Arg.Any<CancellationToken>())
            .Returns([publication]);
        _clientes
            .GetNomesAsync(_tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [clienteId] = "Maria Silva" });

        var result = await _handler.Handle(new ListPublicationsQuery(), CancellationToken.None);

        var item = result.Value.Items.Single();
        item.ClienteId.Should().Be(clienteId);
        item.ClienteNome.Should().Be("Maria Silva");
        await _clientes.Received(1).GetNomesAsync(_tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMultipleStatuses_PassesAllOfThemToTheRepository()
    {
        await _handler.Handle(new ListPublicationsQuery(Status: "approved, Corrected"), CancellationToken.None);

        await _repository.Received(1).SearchAsync(
            _tenantId,
            Arg.Is<PublicationSearchCriteria>(c => c.Statuses!.SequenceEqual(new[] { PublicationStatus.Approved, PublicationStatus.Corrected })),
            0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesTheStructuredFiltersThrough()
    {
        var clienteId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);

        await _handler.Handle(
            new ListPublicationsQuery(IsRead: false, ProcessId: processId, ClienteId: clienteId, Tribunal: "TJSP", From: from, To: to, Busca: "silva"),
            CancellationToken.None);

        await _repository.Received(1).SearchAsync(
            _tenantId,
            Arg.Is<PublicationSearchCriteria>(c =>
                c.IsRead == false && c.ProcessId == processId && c.ClienteId == clienteId && c.Tribunal == "TJSP"
                && c.From == from && c.To == to && c.Busca == "silva"),
            0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownStatus_Fails()
    {
        var result = await _handler.Handle(new ListPublicationsQuery(Status: "nao-existe"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_INVALID_ENUM));
    }

    [Fact]
    public async Task Handle_WithOnlyPendingReview_FiltersByPendingStatus()
    {
        await _handler.Handle(new ListPublicationsQuery(OnlyPendingReview: true), CancellationToken.None);

        await _repository.Received(1).SearchAsync(
            _tenantId,
            Arg.Is<PublicationSearchCriteria>(c => c.Statuses!.Single() == PublicationStatus.PendingReview),
            0, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public async Task Handle_ClampsThePageNumber(int requestedPage, int expectedPage)
    {
        var result = await _handler.Handle(new ListPublicationsQuery(Page: requestedPage), CancellationToken.None);

        result.Value.Page.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(101, 20)]
    [InlineData(50, 50)]
    public async Task Handle_ClampsThePageSize(int requestedPageSize, int expectedPageSize)
    {
        var result = await _handler.Handle(new ListPublicationsQuery(PageSize: requestedPageSize), CancellationToken.None);

        result.Value.PageSize.Should().Be(expectedPageSize);
    }
}
