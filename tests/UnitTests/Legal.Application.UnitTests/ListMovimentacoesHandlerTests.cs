using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Modules.Legal.Application.Mappings;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Legal.Application.UnitTests;

public class ListMovimentacoesHandlerTests
{
    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly IPublicationRepository _publications = Substitute.For<IPublicationRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _processoId = Guid.NewGuid();

    public ListMovimentacoesHandlerTests() => _currentUser.TenantId.Returns(_tenantId);

    private ListMovimentacoesHandler Handler() => new(_processos, _publications, _currentUser);

    private void ProcessoExists() =>
        _processos.GetByIdAsync(ProcessoId.From(_processoId), Arg.Any<CancellationToken>())
            .Returns((Processo)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Processo)));

    [Fact]
    public async Task ProcessoThatDoesNotExist_IsNotFound_AndNoPublicationIsQueried()
    {
        _processos.GetByIdAsync(Arg.Any<ProcessoId>(), Arg.Any<CancellationToken>()).Returns((Processo?)null);

        var result = await Handler().Handle(new ListMovimentacoesQuery(_processoId), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.PROCESS_NOT_FOUND));
        await _publications.DidNotReceiveWithAnyArgs().SearchAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task WithoutATenant_IsRefused()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await Handler().Handle(new ListMovimentacoesQuery(_processoId), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task SearchesThePublicationsOfThatProcess_AndPaginates()
    {
        ProcessoExists();
        _publications.SearchAsync(default, default!, default, default, default).ReturnsForAnyArgs([]);
        _publications.CountSearchAsync(default, default!, default).ReturnsForAnyArgs(120);

        var result = await Handler().Handle(new ListMovimentacoesQuery(_processoId, Page: 2, PageSize: 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(3);
        await _publications.Received(1).SearchAsync(
            _tenantId, Arg.Is<PublicationSearchCriteria>(c => c.ProcessId == _processoId), 50, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClampsAbusivePageSizes()
    {
        ProcessoExists();
        _publications.SearchAsync(default, default!, default, default, default).ReturnsForAnyArgs([]);

        var result = await Handler().Handle(new ListMovimentacoesQuery(_processoId, Page: 0, PageSize: 100000), CancellationToken.None);

        result.Value.PageSize.Should().Be(50);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public void ThePreviewHelper_TruncatesTheSecretText_WithAnEllipsis()
    {
        // O inteiro teor é sigiloso: a movimentação só leva este trecho.
        var preview = PublicationMappingProfile.BuildPreview(new string('x', 1000));

        preview.Length.Should().Be(PublicationMappingProfile.ContentPreviewLength + 1);
        preview.Should().EndWith("…");
        PublicationMappingProfile.BuildPreview("curto").Should().Be("curto");
    }

    [Fact]
    public void MovimentacaoResponse_HasNoFullTextField()
    {
        typeof(MovimentacaoResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().BeEquivalentTo("Id", "Data", "Tipo", "Descricao", "Fonte", "Lida");
    }
}
