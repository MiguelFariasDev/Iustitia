using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "drafting-postgres";
}

/// <summary>A fila contra um Postgres real: exclusão mútua, reserva vencida, backoff e atividade.</summary>
[Collection(PostgresCollection.Name)]
public class FilaPostgresTests(PostgresFixture postgres)
{
    private sealed class MutableClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

        public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    private static async Task SeedAsync(Func<DraftingDbContext> create, MutableClock clock, int count, Guid? tenant = null)
    {
        await using var db = create();
        var fila = new GeracaoPecaFila(db, clock);
        for (var i = 0; i < count; i++)
        {
            fila.Enfileirar(tenant ?? Guid.NewGuid(), QueueItemKind.Geracao, Guid.NewGuid());
        }

        await db.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<ReservedQueueItem>> ReserveAsync(
        Func<DraftingDbContext> create, MutableClock clock, string worker, int max)
    {
        await using var db = create();

        return await new GeracaoPecaFila(db, clock).ReservarAsync(worker, max, TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task VariosWorkersEmParalelo_NuncaReservamOMesmoItem()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 40);

        var resultados = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(i => Task.Run(() => ReserveAsync(create, clock, $"worker-{i}", 10))));

        // O resto que a disputa deixou para trás é recolhido por um worker que chega depois.
        var sobra = await ReserveAsync(create, clock, "retardatario", 100);

        var ids = resultados.SelectMany(r => r).Concat(sobra).Select(item => item.Id).ToList();
        ids.Should().OnlyHaveUniqueItems("a reserva é um UPDATE condicional: só um worker leva cada item");
        ids.Should().HaveCount(40, "todos os itens foram reservados, e nenhum foi perdido");
        await using var db = create();
        (await db.GenerationQueue.CountAsync(i => i.Status == QueueItemStatus.Reservado)).Should().Be(40);
        (await db.GenerationQueue.AllAsync(i => i.Tentativas == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task ReservaRespeitaOLimiteDoLote_EAOrdemDeChegada()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 3);
        await Task.Delay(5);
        await SeedAsync(create, clock, 3);

        var lote = await ReserveAsync(create, clock, "w", 4);

        lote.Should().HaveCount(4);
        lote.Select(i => i.CriadoEm).Should().BeInAscendingOrder();
        await using var db = create();
        (await db.GenerationQueue.CountAsync(i => i.Status == QueueItemStatus.Pendente)).Should().Be(2);
    }

    [Fact]
    public async Task ReservaVencida_VoltaASerElegivel_ContandoNovaTentativa()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 1);
        (await ReserveAsync(create, clock, "caiu", 5)).Should().HaveCount(1);
        (await ReserveAsync(create, clock, "outro", 5)).Should().BeEmpty();

        clock.UtcNow = clock.UtcNow.AddMinutes(16);
        var denovo = await ReserveAsync(create, clock, "outro", 5);

        denovo.Should().ContainSingle().Which.Tentativas.Should().Be(2);
    }

    [Fact]
    public async Task ItemEmBackoff_NaoEElegivelAteAHora()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 1);
        var item = (await ReserveAsync(create, clock, "w", 1)).Single();
        await using (var db = create())
        {
            await new GeracaoPecaFila(db, clock).ReagendarAsync(
                item.Id, clock.UtcNow.AddSeconds(30), "timeout", consumirTentativa: true);
        }

        (await ReserveAsync(create, clock, "w", 1)).Should().BeEmpty();
        await using (var db = create())
        {
            (await new GeracaoPecaFila(db, clock).ContarDisponiveisAsync()).Should().Be(0);
            (await new GeracaoPecaFila(db, clock).ContarTrabalhoAbertoAsync()).Should().Be(1, "em backoff ainda é trabalho aberto: segura a GPU ligada");
        }

        clock.UtcNow = clock.UtcNow.AddSeconds(31);
        (await ReserveAsync(create, clock, "w", 1)).Should().ContainSingle().Which.Tentativas.Should().Be(2);
    }

    [Fact]
    public async Task ReagendarSemConsumirTentativa_DevolveATentativaDaReserva()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 1);
        var item = (await ReserveAsync(create, clock, "w", 1)).Single();
        await using (var db = create())
        {
            await new GeracaoPecaFila(db, clock).ReagendarAsync(item.Id, clock.UtcNow, "gpu fora", consumirTentativa: false);
        }

        var denovo = (await ReserveAsync(create, clock, "w", 1)).Single();

        denovo.Tentativas.Should().Be(1);
    }

    [Fact]
    public async Task Finalizar_LiberaAReservaERegistraOResultado()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 1);
        var item = (await ReserveAsync(create, clock, "w", 1)).Single();

        await using (var db = create())
        {
            await new GeracaoPecaFila(db, clock).FinalizarAsync(item.Id, QueueItemStatus.Falhou, "boom");
        }

        await using var verify = create();
        var salvo = await verify.GenerationQueue.SingleAsync();
        salvo.Status.Should().Be(QueueItemStatus.Falhou);
        salvo.UltimoErro.Should().Be("boom");
        salvo.ConcluidoEm.Should().NotBeNull();
        salvo.ReservadoAte.Should().BeNull();
        (await ReserveAsync(create, clock, "w", 1)).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarPorReferencia_SoAfetaOQueAindaEstaPendente()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 2);
        var reservado = (await ReserveAsync(create, clock, "w", 1)).Single();
        Guid pendenteRef;
        await using (var db = create())
        {
            pendenteRef = (await db.GenerationQueue.AsNoTracking().FirstAsync(i => i.Status == QueueItemStatus.Pendente)).ReferenciaId;
        }

        await using (var db = create())
        {
            var fila = new GeracaoPecaFila(db, clock);
            await fila.CancelarPorReferenciaAsync(pendenteRef);
            await fila.CancelarPorReferenciaAsync(reservado.ReferenciaId);
        }

        await using var verify = create();
        (await verify.GenerationQueue.SingleAsync(i => i.ReferenciaId == pendenteRef)).Status.Should().Be(QueueItemStatus.Cancelado);
        (await verify.GenerationQueue.SingleAsync(i => i.ReferenciaId == reservado.ReferenciaId)).Status
            .Should().Be(QueueItemStatus.Reservado, "quem já está sendo processado é encerrado pelo próprio processador");
    }

    [Fact]
    public async Task ReservarExpirados_PegaSoOsPendentesAntigos()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await SeedAsync(create, clock, 2);
        clock.UtcNow = clock.UtcNow.AddMinutes(61);
        await SeedAsync(create, clock, 1); // novo

        await using var db = create();
        var expirados = await new GeracaoPecaFila(db, clock)
            .ReservarExpiradosAsync("w", clock.UtcNow.AddMinutes(-60), 50, TimeSpan.FromMinutes(15));

        expirados.Should().HaveCount(2);
    }

    [Fact]
    public async Task UltimaAtividade_EOMaiorCarimboDaFila_ESemItensENula()
    {
        var create = await postgres.NewDatabaseAsync();
        var clock = new MutableClock();
        await using (var vazio = create())
        {
            (await new GeracaoPecaFila(vazio, clock).UltimaAtividadeAsync()).Should().BeNull();
        }

        await SeedAsync(create, clock, 1);
        var criado = clock.UtcNow;
        clock.UtcNow = criado.AddMinutes(5);
        var item = (await ReserveAsync(create, clock, "w", 1)).Single();
        clock.UtcNow = criado.AddMinutes(8);
        await using (var db = create())
        {
            await new GeracaoPecaFila(db, clock).FinalizarAsync(item.Id, QueueItemStatus.Concluido, null);
        }

        await using var verify = create();
        var ultima = await new GeracaoPecaFila(verify, clock).UltimaAtividadeAsync();

        ultima.Should().BeCloseTo(criado.AddMinutes(8), TimeSpan.FromMilliseconds(5));
    }
}
