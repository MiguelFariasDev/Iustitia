using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Modules.Drafting.IntegrationTests;

/// <summary>A migration e os scripts SQL de verdade: RLS com <c>app_user</c>, grants e cifra em repouso.</summary>
[Collection(PostgresCollection.Name)]
public class RlsECriptografiaTests(PostgresFixture postgres)
{
    private static DraftingDbContext Contexto(string conexao) => new(
        new DbContextOptionsBuilder<DraftingDbContext>().UseNpgsql(conexao).UseSnakeCaseNamingConvention().Options, new AsyncLocalTenantContext());

    private static async Task<Guid> CriarCasoAsync(string conexao, Guid tenant)
    {
        await using var db = Contexto(conexao);
        var caso = Caso.Criar(tenant, Guid.NewGuid(), Guid.NewGuid(), "Cobrança", "civel", "CLIENTE SIGILOSO", null, null, "CE", DateTimeOffset.UtcNow).Value;
        db.Casos.Add(caso);
        db.CasoMembros.Add(caso.Conceder(Guid.NewGuid(), CasoPermissao.Leitura, Guid.NewGuid(), DateTimeOffset.UtcNow)!);
        var documento = CasoDocumento.Criar(
            CasoDocumentoId.New(), caso, Guid.NewGuid(), "a.pdf", FormatoDocumento.Pdf, 1, Guid.NewGuid().ToString("N"), "k", false, DateTimeOffset.UtcNow);
        documento.AplicarClassificacao("{\"resumo\":\"SIGILOSO\"}", "contrato", "civel");
        db.CasoDocumentos.Add(documento);
        db.CasoDocumentoChunks.Add(new CasoDocumentoChunk(documento, 0, 1, null, "TRECHO SIGILOSO", "h", 1, OrigemTexto.Nativo, null, false));
        await db.SaveChangesAsync();

        return documento.Id.Value;
    }

    private static async Task<long> ContarAsync(string conexao, string tabela, Guid? tenant)
    {
        await using var connection = new NpgsqlConnection(conexao);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = (tenant is null ? string.Empty : $"SET app.current_tenant = '{tenant}'; ") + $"SELECT count(*) FROM {tabela}";

        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Rls_UmEscritorioNaoVeOOutro_ESemTenantNaoSeVeNada()
    {
        var (dono, appUser) = await postgres.NewMigratedDatabaseAsync();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await CriarCasoAsync(dono, a);
        await CriarCasoAsync(dono, a);
        await CriarCasoAsync(dono, b);

        foreach (var tabela in new[] { "casos", "caso_membros", "caso_documentos", "caso_documento_chunks" })
        {
            (await ContarAsync(appUser, tabela, a)).Should().Be(2, tabela);
            (await ContarAsync(appUser, tabela, b)).Should().Be(1, tabela);
            (await ContarAsync(appUser, tabela, null)).Should().Be(0, $"{tabela}: sem tenant na sessão, nega");
        }
    }

    [Fact]
    public async Task Rls_NaoDeixaGravarEmNomeDeOutroEscritorio_NemApagarTrilha()
    {
        var (_, appUser) = await postgres.NewMigratedDatabaseAsync();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        var gravarComoOutro = () => PostgresFixture.ExecutarAsync(appUser, $"""
            SET app.current_tenant = '{a}';
            INSERT INTO drafting_security_events (id, tenant_id, tipo, origem, ocorrido_em) VALUES (gen_random_uuid(), '{b}', 't', 'o', now());
            """);
        var apagarTrilha = () => PostgresFixture.ExecutarAsync(appUser, $"SET app.current_tenant = '{a}'; DELETE FROM drafting_security_events;");

        (await gravarComoOutro.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
        (await apagarTrilha.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task NoBanco_TextoDoCasoEstaCifrado_EAAplicacaoLeDeVolta()
    {
        var (dono, _) = await postgres.NewMigratedDatabaseAsync();
        var documento = await CriarCasoAsync(dono, Guid.NewGuid());

        await using (var connection = new NpgsqlConnection(dono))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT texto FROM caso_documento_chunks) || (SELECT classificacao_json FROM caso_documentos) || (SELECT cliente_nome FROM casos)";
            ((string)(await command.ExecuteScalarAsync())!).Should().StartWith("enc:").And.NotContain("SIGILOSO");
        }

        await using var db = Contexto(dono);
        (await new CasoDocumentoRepository(db).ListarChunksAsync(CasoDocumentoId.From(documento))).Single().Texto.Should().Be("TRECHO SIGILOSO");
    }

    [Fact]
    public async Task SubstituirChunks_ComOsMesmosIds_Regrava_ERemoverDocumentoLevaOsTrechos()
    {
        var (dono, _) = await postgres.NewMigratedDatabaseAsync();
        var id = CasoDocumentoId.From(await CriarCasoAsync(dono, Guid.NewGuid()));

        await using (var db = Contexto(dono))
        {
            var repository = new CasoDocumentoRepository(db);
            var documento = (await repository.ObterAsync(id))!;
            await repository.SubstituirChunksAsync(id, [new CasoDocumentoChunk(documento, 0, 1, null, "NOVO", "h2", 1, OrigemTexto.Nativo, null, false)]);
            await db.SaveChangesAsync();
        }

        await using (var db = Contexto(dono))
        {
            var repository = new CasoDocumentoRepository(db);
            (await repository.ListarChunksAsync(id)).Single().Texto.Should().Be("NOVO");
            repository.Remover((await repository.ObterAsync(id))!);
            await db.SaveChangesAsync();
            (await db.CasoDocumentoChunks.CountAsync()).Should().Be(0);
        }
    }
}
