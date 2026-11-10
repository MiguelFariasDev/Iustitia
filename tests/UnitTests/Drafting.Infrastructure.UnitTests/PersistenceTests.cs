using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Advocacia.Modules.Drafting.Infrastructure.Fila;
using Microsoft.Extensions.Options;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

/// <summary>
/// Persistência de ponta a ponta contra o provedor em memória do EF. Ele NÃO aplica RLS nem
/// índices do Postgres — isso é coberto pela migração SQL 016 —, mas exercita mapeamento,
/// conversões de id, filhos (fontes e eventos), filtro global de tenant e versionamento.
/// </summary>
public class PersistenceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public bool HasTenant => true;

        public Guid TenantId { get; } = tenantId;

        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
    }

    private sealed class FixedUser(Guid tenantId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => User;

        public Guid? TenantId { get; } = tenantId;

        public string? Email => "advogada@teste.com";

        public IReadOnlyCollection<string> Roles => ["Lawyer"];

        public bool IsInRole(string role) => role == "Lawyer";
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        public DateOnly Today => new(2026, 11, 10);
    }

    private static DbContextOptions<DraftingDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<DraftingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private sealed class Scope(DraftingDbContext db, LegalDocumentService service, DraftingAuditService audit) : IAsyncDisposable
    {
        public DraftingDbContext Db { get; } = db;

        public LegalDocumentService Service { get; } = service;

        public DraftingAuditService Audit { get; } = audit;

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static Scope Open(DbContextOptions<DraftingDbContext> options, Guid tenantId)
    {
        var db = new DraftingDbContext(options, new FixedTenant(tenantId));
        var repository = new LegalDocumentRepository(db);
        var unitOfWork = new DraftingUnitOfWork(db);
        var clock = new FixedClock();

        return new Scope(
            db,
            new LegalDocumentService(
                repository, new GeracaoPecaFila(db, clock), unitOfWork, new FixedUser(tenantId), clock,
                Options.Create(new CustoOptions { GpuUsdPorHora = 0.40m })),
            new DraftingAuditService(repository, unitOfWork, clock, NullLogger<DraftingAuditService>.Instance));
    }

    private static PecaInput Input(Guid? origem = null) =>
        new("peticao_inicial", "civel", "Fatos.", "Pedidos.", "Tom firme.", [Guid.Parse("33333333-3333-3333-3333-333333333333")], origem);

    private static RetrievedChunk Chunk() =>
        new("11111111-2222-3333-4444-555555555555", "Art. 186: ato ilícito.", 0.91,
            new ChunkMetadata(Guid.Parse("33333333-3333-3333-3333-333333333333"), "contrato.pdf", 3, "contrato", "civel", "TJSP",
                new DateOnly(2024, 5, 1), "https://exemplo.com/c", "H1"));

    [Fact]
    public async Task PersisteASolicitacaoComTenantUsuarioEEntradaSanitizada()
    {
        var options = NewDatabase();
        Guid id;
        await using (var scope = Open(options, TenantA))
        {
            var service = scope.Service;
            var created = await service.CreateAndEnqueueAsync(Input());
            created.IsSuccess.Should().BeTrue();
            id = created.Value.Id.Value;
        }

        await using var scopeV = Open(options, TenantA);
        var verify = scopeV.Db;
        var request = await verify.LegalDocumentRequests.SingleAsync(r => r.Id == LegalDocumentRequestId.From(id));
        request.TenantId.Should().Be(TenantA);
        request.UsuarioId.Should().Be(User);
        request.TipoPeca.Should().Be("peticao_inicial");
        request.Area.Should().Be("civel");
        request.Fatos.Should().Be("Fatos.");
        request.InstrucoesAdicionais.Should().Be("Tom firme.");
        request.Status.Should().Be(LegalDocumentStatus.Pendente);
        request.Versao.Should().Be(1);
        request.DocumentosIdsJson.Should().Contain("33333333-3333-3333-3333-333333333333");

        // E o item da fila foi gravado na MESMA transação, apontando para a solicitação.
        var item = await verify.GenerationQueue.SingleAsync();
        item.ReferenciaId.Should().Be(id);
        item.TenantId.Should().Be(TenantA);
        item.Tipo.Should().Be(QueueItemKind.Geracao);
        item.Status.Should().Be(QueueItemStatus.Pendente);
    }

    [Fact]
    public async Task CicloCompleto_GravaTextoModeloTempoFontesEEventosEPodeSerRelido()
    {
        var options = NewDatabase();
        Guid id;
        await using (var scope = Open(options, TenantA))
        {
            var (service, audit) = (scope.Service, scope.Audit);
            var request = (await service.CreateAndEnqueueAsync(Input())).Value;
            id = request.Id.Value;
            await audit.RecordAsync(request, "Recebida", new { documentos = 1 });
            await service.StartGenerationAsync(request, SystemPrompt.Versao, "qwen3:8b");
            await service.CompleteAsync(request, "Minuta pronta.", 1234, 100, 200, [(Chunk(), true)], ["Aviso de teste"]);
            await audit.RecordAsync(request, "GeracaoConcluida", new { tempoMs = 1234 });
        }

        await using var scopeV = Open(options, TenantA);
        var verify = scopeV.Db;
        var saved = await verify.LegalDocumentRequests
            .Include(r => r.Sources)
            .Include(r => r.Events)
            .SingleAsync(r => r.Id == LegalDocumentRequestId.From(id));

        saved.Status.Should().Be(LegalDocumentStatus.Concluido);
        saved.TextoGerado.Should().Be("Minuta pronta.");
        saved.ModeloUtilizado.Should().Be("qwen3:8b");
        saved.PromptVersao.Should().Be(SystemPrompt.Versao);
        saved.TempoGeracaoMs.Should().Be(1234);
        saved.TokensEntrada.Should().Be(100);
        saved.TokensSaida.Should().Be(200);
        // 1,234 s a US$ 0,40/h
        saved.CustoEstimadoUsd.Should().BeApproximately(0.000137m, 0.000001m);
        saved.AvisosJson.Should().Contain("Aviso de teste");
        saved.Progresso.Should().Be(100);
        saved.ConcluidoEm.Should().NotBeNull();

        var fonte = saved.Sources.Should().ContainSingle().Subject;
        fonte.NomeArquivo.Should().Be("contrato.pdf");
        fonte.Pagina.Should().Be(3);
        fonte.Score.Should().BeApproximately(0.91, 0.0001);
        fonte.UtilizadaNaGeracao.Should().BeTrue();
        fonte.TenantId.Should().Be(TenantA);
        fonte.MetadadosJson.Should().Contain("TJSP").And.Contain("2024-05-01");

        saved.Events.Select(e => e.Tipo).Should().BeEquivalentTo("Recebida", "GeracaoConcluida");
        saved.Events.Should().OnlyContain(e => e.TenantId == TenantA);
    }

    [Fact]
    public async Task Falha_GravaOMotivoETextoParcialSemApagarAsFontes()
    {
        var options = NewDatabase();
        Guid id;
        await using (var scope = Open(options, TenantA))
        {
            var service = scope.Service;
            var request = (await service.CreateAndEnqueueAsync(Input())).Value;
            id = request.Id.Value;
            await service.FailAsync(request, "TimeoutException: prazo", "Texto até aqui.", 600_000, [(Chunk(), false)]);
        }

        await using var scopeV = Open(options, TenantA);
        var verify = scopeV.Db;
        var saved = await verify.LegalDocumentRequests.Include(r => r.Sources).SingleAsync(r => r.Id == LegalDocumentRequestId.From(id));

        saved.Status.Should().Be(LegalDocumentStatus.Falhou);
        saved.Erro.Should().Be("TimeoutException: prazo");
        saved.TextoGerado.Should().Be("Texto até aqui.");
        saved.Sources.Should().ContainSingle().Which.UtilizadaNaGeracao.Should().BeFalse();
    }

    [Fact]
    public async Task Regerar_CriaVersaoSeguinteLigadaAAnteriorSemSobrescreverAPrimeira()
    {
        var options = NewDatabase();
        Guid primeiraId;
        Guid segundaId;
        await using (var scope = Open(options, TenantA))
        {
            var service = scope.Service;
            var primeira = (await service.CreateAndEnqueueAsync(Input())).Value;
            await service.CompleteAsync(primeira, "Versão 1.", 10, null, null, [], []);
            primeiraId = primeira.Id.Value;
            var segunda = (await service.CreateAndEnqueueAsync(Input(origem: primeiraId))).Value;
            segundaId = segunda.Id.Value;
        }

        await using var scopeV = Open(options, TenantA);
        var verify = scopeV.Db;
        var v1 = await verify.LegalDocumentRequests.SingleAsync(r => r.Id == LegalDocumentRequestId.From(primeiraId));
        var v2 = await verify.LegalDocumentRequests.SingleAsync(r => r.Id == LegalDocumentRequestId.From(segundaId));

        v1.Versao.Should().Be(1);
        v1.TextoGerado.Should().Be("Versão 1.");
        v2.Versao.Should().Be(2);
        v2.RequestOrigemId.Should().Be(LegalDocumentRequestId.From(primeiraId));
    }

    [Fact]
    public async Task OutroEscritorioNaoVeNemVersionaASolicitacaoAlheia()
    {
        var options = NewDatabase();
        Guid idDeA;
        await using (var scope = Open(options, TenantA))
        {
            var service = scope.Service;
            idDeA = (await service.CreateAndEnqueueAsync(Input())).Value.Id.Value;
        }

        await using var scopeB = Open(options, TenantB);
        var (dbB, serviceB) = (scopeB.Db, scopeB.Service);
        (await dbB.LegalDocumentRequests.CountAsync()).Should().Be(0);

        var tentativa = await serviceB.CreateAndEnqueueAsync(Input(origem: idDeA));

        tentativa.IsFailure.Should().BeTrue();
        tentativa.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
    }

    [Fact]
    public async Task SemTenantNoToken_NaoCria()
    {
        var options = NewDatabase();
        await using var db = new DraftingDbContext(options, new FixedTenant(TenantA));
        var repository = new LegalDocumentRepository(db);
        var service = new LegalDocumentService(
            repository, new GeracaoPecaFila(db, new FixedClock()), new DraftingUnitOfWork(db), new NoTenantUser(), new FixedClock(),
            Options.Create(new CustoOptions()));

        var result = await service.CreateAndEnqueueAsync(Input());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        (await db.LegalDocumentRequests.CountAsync()).Should().Be(0);
    }

    private sealed class NoTenantUser : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => User;

        public Guid? TenantId => null;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => [];

        public bool IsInRole(string role) => false;
    }
}
