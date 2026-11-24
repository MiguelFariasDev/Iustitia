using System.Data;
using System.Text;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistência do acervo normativo + busca por palavra-chave. A palavra-chave usa o full-text do
/// PostgreSQL (<c>tsvector</c> 'portuguese' + <c>ts_rank_cd</c>). ATENÇÃO: isto NÃO é BM25 — o PostgreSQL
/// não tem BM25 nativo; ts_rank_cd é um ranking por frequência/proximidade, suficiente para o papel de
/// "segundo sinal" que a busca híbrida precisa. BM25 de verdade exigiria extensão (ParadeDB/pg_search) ou
/// o modo esparso do Qdrant.
/// </summary>
public sealed class NormaRepository(DraftingDbContext db) : INormaStore, INormaSearchIndex
{
    public async Task<IReadOnlyList<NormaEstado>> ListarEstadoAsync(CancellationToken cancellationToken = default) =>
        await db.Normas.AsNoTracking()
            .Select(n => new NormaEstado(n.Id, n.RepoPath, n.HashSha256, n.Status, n.Colecao))
            .ToListAsync(cancellationToken);

    public Task<NormaJuridica?> ObterPorCaminhoAsync(string repoPath, CancellationToken cancellationToken = default) =>
        db.Normas.FirstOrDefaultAsync(n => n.RepoPath == repoPath, cancellationToken);

    public Task<NormaVersao?> ObterVersaoAsync(Guid normaId, int numero, CancellationToken cancellationToken = default) =>
        db.NormaVersoes.FirstOrDefaultAsync(v => v.NormaJuridicaId == normaId && v.Numero == numero, cancellationToken);

    public void Adicionar(NormaJuridica norma) => db.Normas.Add(norma);

    public void Adicionar(NormaVersao versao) => db.NormaVersoes.Add(versao);

    public void Adicionar(IngestaoJob job) => db.IngestaoJobs.Add(job);

    public void Adicionar(IEnumerable<NormaChunk> chunks) => db.NormaChunks.AddRange(chunks);

    public Task RemoverChunksDaVersaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default) =>
        db.NormaChunks.Where(c => c.NormaJuridicaId == normaId && c.Versao == versao).ExecuteDeleteAsync(cancellationToken);

    public async Task ConcluirIndexacaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default)
    {
        await db.NormaChunks
            .Where(c => c.NormaJuridicaId == normaId && c.Versao == versao)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.Status, ChunkStatus.Indexado)
                    .SetProperty(c => c.QdrantPointId, c => c.Id.ToString()),
                cancellationToken);

        // A versão anterior só sai depois de a nova estar indexada (a busca por palavra-chave nunca fica sem a norma).
        await db.NormaChunks
            .Where(c => c.NormaJuridicaId == normaId && c.Versao != versao)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<IngestaoJob?> ObterJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.IngestaoJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<IngestaoJob?> ObterJobPendenteAsync(CancellationToken cancellationToken = default) =>
        db.IngestaoJobs.Where(j => j.Status == IngestaoJobStatus.Pendente).OrderBy(j => j.IniciadoEm).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<IngestaoJob>> ListarJobsAsync(int limite, CancellationToken cancellationToken = default) =>
        await db.IngestaoJobs.AsNoTracking().OrderByDescending(j => j.IniciadoEm).Take(Math.Clamp(limite, 1, 100)).ToListAsync(cancellationToken);

    public Task<DateTimeOffset?> UltimaSincronizacaoAsync(CancellationToken cancellationToken = default) =>
        db.IngestaoJobs.Where(j => j.Status != IngestaoJobStatus.Pendente).MaxAsync(j => (DateTimeOffset?)j.IniciadoEm, cancellationToken);

    public Task<NormaJuridica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Normas.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<int> SalvarAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    public Task<bool> UrnExisteAsync(string urn, CancellationToken cancellationToken = default) =>
        db.Normas.AnyAsync(n => n.Urn == urn && n.Status != NormaStatus.Removida, cancellationToken);

    // ------------------------------------------------------------ palavra-chave

    public async Task<IReadOnlyList<VectorHit>> BuscarPalavrasChaveAsync(
        NormaFiltro filtro, string consulta, int limit, CancellationToken cancellationToken = default)
    {
        var termos = NormaTexto.Termos(consulta);
        if (termos.Count == 0)
        {
            return [];
        }

        var (sql, parametros) = MontarSql(filtro, termos, limit);
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parametro in parametros)
        {
            command.Parameters.Add(parametro);
        }

        var hits = new List<VectorHit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string? Str(string col) => reader.IsDBNull(reader.GetOrdinal(col)) ? null : reader.GetString(reader.GetOrdinal(col));
            var assuntos = reader.IsDBNull(reader.GetOrdinal("assuntos")) ? [] : reader.GetFieldValue<string[]>(reader.GetOrdinal("assuntos"));
            DateOnly? data = reader.IsDBNull(reader.GetOrdinal("data")) ? null : reader.GetFieldValue<DateOnly>(reader.GetOrdinal("data"));

            var payload = new NormaPayload(
                reader.GetGuid(reader.GetOrdinal("norma_id")),
                Str("urn"),
                Str("titulo")!,
                Str("tipo")!,
                Str("numero"),
                data,
                Str("situacao")!,
                reader.GetBoolean(reader.GetOrdinal("revogada")),
                Str("versao_texto")!,
                reader.GetInt32(reader.GetOrdinal("versao_numero")),
                Str("esfera")!,
                Str("uf"),
                Str("municipio"),
                Str("area"),
                Str("tribunal"),
                assuntos,
                Str("colecao")!,
                Str("arquivo_markdown_path")!,
                Str("fonte_oficial_url")!,
                reader.IsDBNull(reader.GetOrdinal("pagina")) ? null : reader.GetInt32(reader.GetOrdinal("pagina")),
                Str("secao"),
                Str("artigo"),
                Str("paragrafo"),
                Str("inciso"),
                Str("hash_conteudo")!,
                Str("texto")!);

            hits.Add(payload.ToHit(reader.GetGuid(reader.GetOrdinal("chunk_id")).ToString(), reader.GetDouble(reader.GetOrdinal("score"))));
        }

        return hits;
    }

    /// <summary>
    /// Consulta em OU (qualquer termo) ranqueada por <c>ts_rank_cd</c>. Os termos já vêm saneados (só letras e
    /// dígitos) de <see cref="NormaTexto.Termos"/>, mas mesmo assim vão como PARÂMETRO, nunca concatenados.
    /// </summary>
    internal static (string Sql, List<NpgsqlParameter> Parametros) MontarSql(NormaFiltro f, IReadOnlyList<string> termos, int limit)
    {
        var parametros = new List<NpgsqlParameter>
        {
            new("tsq", string.Join(" | ", termos)),
            new("lim", Math.Clamp(limit, 1, 200)),
            new("colecoes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = f.ColecoesEfetivas.ToArray() },
        };

        var where = new StringBuilder(
            "c.tsv @@ q.query AND c.status = 'Indexado' AND n.status <> 'Removida' AND n.colecao = ANY(@colecoes)");

        void Any(string coluna, string nome, IReadOnlyList<string>? valores, bool ilike = false, bool permiteNulo = false)
        {
            if (valores is not { Count: > 0 })
            {
                return;
            }

            var valoresSql = ilike ? valores.Select(v => $"%{v}%").ToArray() : valores.Select(v => v.ToLowerInvariant()).ToArray();
            parametros.Add(new NpgsqlParameter(nome, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = valoresSql });
            var cond = ilike ? $"{coluna} ILIKE ANY(@{nome})" : $"lower({coluna}) = ANY(@{nome})";
            where.Append(" AND (").Append(cond);
            if (permiteNulo)
            {
                where.Append(" OR ").Append(coluna).Append(" IS NULL");
            }

            where.Append(')');
        }

        Any("n.tipo", "tipos", f.Tipos, ilike: true);
        Any("n.tribunal", "tribunais", f.Tribunais);
        Any("n.esfera", "esferas", f.Esferas);
        // UF/município restringem só quem TEM UF/município (federal e tribunais seguem valendo).
        Any("n.uf", "ufs", f.Ufs, permiteNulo: true);
        Any("n.municipio", "municipios", f.Municipios, permiteNulo: true);

        if (f.AreaEstrita && !string.IsNullOrWhiteSpace(f.Area))
        {
            parametros.Add(new NpgsqlParameter("area", f.Area));
            where.Append(" AND n.area = @area");
        }

        if (!f.IncluirRevogadas)
        {
            where.Append(" AND n.revogada = false");
        }

        if (f.DataDe is { } de)
        {
            parametros.Add(new NpgsqlParameter("dataDe", NpgsqlDbType.Date) { Value = de });
            where.Append(" AND n.data >= @dataDe");
        }

        if (f.DataAte is { } ate)
        {
            parametros.Add(new NpgsqlParameter("dataAte", NpgsqlDbType.Date) { Value = ate });
            where.Append(" AND n.data <= @dataAte");
        }

        if (f.NormaIds is { Count: > 0 } ids)
        {
            parametros.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() });
            where.Append(" AND n.id = ANY(@ids)");
        }

        if (f.Assuntos is { Count: > 0 } assuntos)
        {
            parametros.Add(new NpgsqlParameter("assuntos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = assuntos.ToArray() });
            where.Append(" AND n.assuntos && @assuntos");
        }

        var sql = $"""
            SELECT c.id AS chunk_id, c.texto, c.pagina, c.secao, c.artigo, c.paragrafo, c.inciso, c.hash_sha256 AS hash_conteudo,
                   n.id AS norma_id, n.urn, n.titulo, n.tipo, n.numero, n.data, n.situacao, n.revogada, n.versao_texto,
                   n.versao_atual AS versao_numero, n.esfera, n.uf, n.municipio, n.area, n.tribunal, n.assuntos, n.colecao,
                   n.arquivo_markdown_path, n.fonte_oficial_url,
                   ts_rank_cd(c.tsv, q.query)::float8 AS score
            FROM norma_chunks c
            JOIN normas_juridicas n ON n.id = c.norma_juridica_id
            CROSS JOIN (SELECT to_tsquery('portuguese', @tsq) AS query) q
            WHERE {where}
            ORDER BY score DESC
            LIMIT @lim
            """;

        return (sql, parametros);
    }
}
