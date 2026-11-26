using System.Text;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistência do acervo normativo, busca por referência exata (norma + artigo, súmula, tema) e full-text de
/// reserva. O BM25 é do Qdrant (vetor esparso com IDF); o full-text daqui (<c>tsvector</c> 'portuguese' +
/// <c>ts_rank_cd</c>, que NÃO é BM25) só entra quando ele está fora do ar.
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

        return await ConsultarAsync(sql, parametros, cancellationToken);
    }

    public async Task<IReadOnlyList<VectorHit>> BuscarReferenciasAsync(
        IReadOnlyList<ReferenciaLegal> referencias, int limite, CancellationToken cancellationToken = default)
    {
        if (referencias.Count == 0)
        {
            return [];
        }

        var (sql, parametros) = MontarSqlReferencias(referencias.Take(20).ToList(), limite);

        return await ConsultarAsync(sql, parametros, cancellationToken);
    }

    /// <summary>
    /// A conexão é aberta PELO EF, e não por ADO direto: só assim o interceptor que define o tenant da sessão
    /// (RLS) roda. Abrir por fora deixaria a conexão sem tenant para tudo que o mesmo contexto gravasse depois.
    /// </summary>
    private async Task<IReadOnlyList<VectorHit>> ConsultarAsync(
        string sql, List<NpgsqlParameter> parametros, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(parametros.ToArray());

            return await LerAsync(command, cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Uma condição por referência, tudo por PARÂMETRO. O artigo é comparado sem ponto e sem "º" ("1.186" e
    /// "5º" como 1186 e 5), e norma citada sem artigo só devolve os primeiros trechos dela.
    /// </summary>
    internal static (string Sql, List<NpgsqlParameter> Parametros) MontarSqlReferencias(
        IReadOnlyList<ReferenciaLegal> referencias, int limite)
    {
        const string artigo = "upper(regexp_replace(c.artigo, '[.ºo°]', '', 'g'))";
        var parametros = new List<NpgsqlParameter> { new("lim", Math.Clamp(limite, 1, 200)) };
        var condicoes = new List<string>();
        var pontos = new List<string>();

        for (var i = 0; i < referencias.Count; i++)
        {
            var r = referencias[i];
            var partes = new List<string>();
            if (r.Constituicao)
            {
                partes.Add($"n.colecao = '{Colecoes.ConstituicaoFederal}'");
                // ADCT reinicia a numeração: "art. 5º da CF" é o do corpo permanente.
                partes.Add("(c.secao IS NULL OR c.secao NOT ILIKE 'ATO DAS%')");
            }
            else
            {
                parametros.Add(new NpgsqlParameter($"n{i}", r.Numero ?? string.Empty));
                partes.Add($"replace(n.numero, '.', '') = @n{i}");
                partes.Add(r.Tipo switch
                {
                    TipoReferencia.Sumula => $"n.colecao = '{Colecoes.Jurisprudencia}' AND n.tipo ILIKE '%súmula%'"
                        + (r.Vinculante ? " AND n.tipo ILIKE '%vinculante%'" : " AND n.tipo NOT ILIKE '%vinculante%'"),
                    TipoReferencia.Tema => $"n.colecao = '{Colecoes.Jurisprudencia}' AND (n.tipo ILIKE '%tema%' OR n.tipo ILIKE '%repercuss%')",
                    _ => $"n.colecao = '{Colecoes.LeisFederais}'",
                });
                if (r.Tribunal is not null)
                {
                    parametros.Add(new NpgsqlParameter($"t{i}", r.Tribunal));
                    partes.Add($"upper(n.tribunal) = @t{i}");
                }
            }

            if (r.Artigo is not null)
            {
                parametros.Add(new NpgsqlParameter($"a{i}", r.Artigo));
                partes.Add($"{artigo} = @a{i}");
            }
            else
            {
                partes.Add("c.ordem < 3");
            }

            var condicao = "(" + string.Join(" AND ", partes) + ")";
            condicoes.Add(condicao);
            pontos.Add($"CASE WHEN {condicao} THEN {(r.Artigo is null && r.Tipo == TipoReferencia.Norma ? 1 : 3)} ELSE 0 END");
        }

        var pontuacao = string.Join(" + ", pontos);
        var filtros = string.Join(" OR ", condicoes);
        var sql = $"""
            SELECT {Colunas},
                   ({pontuacao})::float8 AS score
            FROM norma_chunks c
            JOIN normas_juridicas n ON n.id = c.norma_juridica_id
            WHERE c.status = 'Indexado' AND n.status <> 'Removida' AND ({filtros})
            ORDER BY score DESC, n.revogada, c.ordem
            LIMIT @lim
            """;

        return (sql, parametros);
    }

    private const string Colunas = """
        c.id AS chunk_id, c.texto, c.pagina, c.secao, c.artigo, c.paragrafo, c.inciso, c.hash_sha256 AS hash_conteudo,
               n.id AS norma_id, n.urn, n.titulo, n.tipo, n.numero, n.data, n.situacao, n.revogada, n.versao_texto,
               n.versao_atual AS versao_numero, n.esfera, n.uf, n.municipio, n.area, n.tribunal, n.assuntos, n.colecao,
               n.repo_path AS arquivo_markdown_path, n.fonte_oficial_url
        """;

    private static async Task<IReadOnlyList<VectorHit>> LerAsync(
        System.Data.Common.DbCommand command, CancellationToken cancellationToken)
    {
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

        // Área desconhecida (nula) não é "outra área": passa.
        Any("n.area", "areas", f.Areas, permiteNulo: true);

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
            SELECT {Colunas},
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
