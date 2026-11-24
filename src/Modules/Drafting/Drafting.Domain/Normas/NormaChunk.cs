using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Normas;

/// <summary>
/// Trecho de uma versão da norma. O texto fica aqui (busca por palavra-chave via full-text do PostgreSQL)
/// e no payload do Qdrant (busca vetorial); o Markdown integral fica só no object storage.
/// </summary>
public sealed class NormaChunk : Entity<Guid>
{
    public Guid NormaJuridicaId { get; private set; }

    /// <summary>Versão da norma (<see cref="NormaJuridica.VersaoAtual"/>) a que este trecho pertence.</summary>
    public int Versao { get; private set; }

    public int Ordem { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public int? Pagina { get; private set; }

    public string? Secao { get; private set; }

    public string? Artigo { get; private set; }

    public string? Paragrafo { get; private set; }

    public string? Inciso { get; private set; }

    public int Tokens { get; private set; }

    public string HashSha256 { get; private set; } = string.Empty;

    /// <summary>Id do ponto no Qdrant (o próprio <see cref="Entity{TId}.Id"/> em texto).</summary>
    public string? QdrantPointId { get; private set; }

    public ChunkStatus Status { get; private set; }

    public DateTimeOffset DataIngestao { get; private set; }

    private NormaChunk()
    {
        // EF Core.
    }

    public NormaChunk(
        Guid normaId, int versao, int ordem, string texto, string? secao, string? artigo, string? paragrafo,
        string? inciso, int tokens, string hash, DateTimeOffset agora)
        : base(IdPara(normaId, versao, ordem))
    {
        NormaJuridicaId = normaId;
        Versao = versao;
        Ordem = ordem;
        Texto = texto;
        Secao = secao;
        Artigo = artigo;
        Paragrafo = paragrafo;
        Inciso = inciso;
        Tokens = tokens;
        HashSha256 = hash;
        Status = ChunkStatus.Pendente;
        DataIngestao = agora;
    }

    /// <summary>
    /// Id DETERMINÍSTICO (norma + versão + ordem): reprocessar a mesma versão sobrescreve os mesmos pontos
    /// do Qdrant em vez de duplicá-los, então uma indexação interrompida pode simplesmente ser refeita.
    /// </summary>
    public static Guid IdPara(Guid normaId, int versao, int ordem)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{normaId:N}:{versao}:{ordem}"));

        return new Guid(bytes.AsSpan(0, 16));
    }

    public void MarcarIndexado()
    {
        QdrantPointId = Id.ToString();
        Status = ChunkStatus.Indexado;
    }

    public void MarcarRemovido() => Status = ChunkStatus.Removido;
}
