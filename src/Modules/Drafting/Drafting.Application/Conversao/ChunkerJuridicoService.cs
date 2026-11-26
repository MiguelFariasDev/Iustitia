using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <summary>Divisão consciente da estrutura: normas por artigo (hierárquico), anexos por página/seção/cláusula.</summary>
public interface IChunkerJuridicoService
{
    IReadOnlyList<ChunkDraft> DividirNorma(NormaMetadata meta, string corpoMarkdown);

    IReadOnlyList<TrechoDocumento> DividirDocumento(DocumentoConvertido documento);
}

public sealed class ChunkerJuridicoService(IOptions<ChunkingOptions> options) : IChunkerJuridicoService
{
    private readonly NormaChunker _normas = new(options.Value);
    private readonly DocumentoCasoChunker _documentos = new(options.Value);

    public IReadOnlyList<ChunkDraft> DividirNorma(NormaMetadata meta, string corpoMarkdown) => _normas.Dividir(meta, corpoMarkdown);

    public IReadOnlyList<TrechoDocumento> DividirDocumento(DocumentoConvertido documento) => _documentos.Dividir(documento);
}
