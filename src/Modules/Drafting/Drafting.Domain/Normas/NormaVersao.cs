using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Normas;

/// <summary>
/// Histórico do texto de uma norma no NOSSO acervo (uma linha por hash distinto). Permite auditar e
/// reverter: o Markdown de cada versão antiga fica preservado no object storage.
/// </summary>
public sealed class NormaVersao : Entity<Guid>
{
    public Guid NormaJuridicaId { get; private set; }

    public int Numero { get; private set; }

    public string VersaoTexto { get; private set; } = string.Empty;

    /// <summary>Data da norma segundo o YAML (início de vigência quando é a data do ato; nem sempre é).</summary>
    public DateOnly? DataVigencia { get; private set; }

    public DateOnly? DataRevogacao { get; private set; }

    public string Situacao { get; private set; } = string.Empty;

    public string HashSha256 { get; private set; } = string.Empty;

    public string ArquivoMarkdownPath { get; private set; } = string.Empty;

    public string? CommitOrigem { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Quando uma versão mais nova a substituiu; nulo na versão atual.</summary>
    public DateTimeOffset? SubstituidaEm { get; private set; }

    private NormaVersao()
    {
        // EF Core.
    }

    public NormaVersao(
        Guid normaId, int numero, NormaMetadata metadata, string hash, string arquivoMarkdownPath,
        string? commitOrigem, DateTimeOffset agora)
        : base(Guid.NewGuid())
    {
        NormaJuridicaId = normaId;
        Numero = numero;
        VersaoTexto = metadata.VersaoTexto;
        DataVigencia = metadata.Data;
        Situacao = metadata.Situacao;
        HashSha256 = hash;
        ArquivoMarkdownPath = arquivoMarkdownPath;
        CommitOrigem = commitOrigem;
        CriadaEm = agora;
    }

    public void Substituir(string novoCaminhoArquivo, DateTimeOffset agora)
    {
        ArquivoMarkdownPath = novoCaminhoArquivo;
        SubstituidaEm = agora;
    }

    public void RegistrarRevogacao(DateOnly? data) => DataRevogacao = data;
}
