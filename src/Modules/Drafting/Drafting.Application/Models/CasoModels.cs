namespace Advocacia.Modules.Drafting.Application.Models;

public sealed record CriarCasoRequest(
    Guid ClienteId,
    string Titulo,
    string Area,
    string? ClienteNome = null,
    Guid? ProcessoId = null,
    string? NumeroProcesso = null,
    string? Uf = null);

public sealed record AtualizarCasoRequest(string Titulo, string Area, string? NumeroProcesso = null, string? Uf = null, bool? Encerrado = null);

/// <param name="Permissao">"leitura" ou "escrita".</param>
public sealed record ConcederAcessoRequest(Guid UsuarioId, string Permissao);

public sealed record MembroResponse(Guid UsuarioId, string Permissao, DateTimeOffset ConcedidoEm);

public sealed record CasoResponse(
    Guid Id,
    Guid ClienteId,
    string? ClienteNome,
    string Titulo,
    string Area,
    Guid? ProcessoId,
    string? NumeroProcesso,
    string? Uf,
    string Status,
    Guid UsuarioIdResponsavel,
    bool PodeEscrever,
    DateTimeOffset CriadoEm,
    IReadOnlyList<MembroResponse> Membros);

public sealed record UploadAnexoResponse(Guid Id, string Status, string Mensagem);

/// <summary>Nunca devolve chave de storage, hash de tenant nem detalhe técnico da falha.</summary>
public sealed record AnexoResponse(
    Guid Id,
    Guid CasoId,
    string NomeArquivo,
    string Formato,
    long TamanhoBytes,
    string Status,
    string? Mensagem,
    string Origem,
    string? ConfiancaOcr,
    string? TipoDocumento,
    string? Area,
    DateOnly? DataDocumento,
    int Paginas,
    int Trechos,
    bool Restrito,
    bool RequerRevisaoHumana,
    bool Revisado,
    bool ConteudoSuspeito,
    ClassificacaoDocumento? Classificacao,
    DateTimeOffset CriadoEm,
    DateTimeOffset? IndexadoEm);

public sealed record AnexoConteudo(byte[] Conteudo, string NomeArquivo, string ContentType);

/// <summary>
/// Classificação assistida. Apoio à busca e à revisão — NUNCA verdade absoluta: valores e datas que não
/// aparecem no texto do documento são descartados antes de chegar aqui.
/// </summary>
public sealed record ClassificacaoDocumento(
    string TipoDocumento,
    string? Area,
    IReadOnlyList<string> Partes,
    IReadOnlyList<string> Datas,
    IReadOnlyList<string> Valores,
    IReadOnlyList<string> Obrigacoes,
    IReadOnlyList<string> Clausulas,
    IReadOnlyList<string> Fatos,
    IReadOnlyList<string> Provas,
    IReadOnlyList<string> Riscos,
    string Resumo,
    double Confianca,
    bool RequerOCR,
    bool RequerRevisaoHumana,
    string Metodo);

/// <summary>Consulta estruturada que guia a busca. Dispositivos e tribunais são PISTAS de busca, nunca fundamento citável.</summary>
public sealed record ConsultaEstruturada(
    string TipoPeca,
    string Area,
    string Tese,
    IReadOnlyList<string> Temas,
    IReadOnlyList<string> DispositivosSuspeitos,
    IReadOnlyList<string> TribunalPreferido,
    string? Periodo,
    string Fatos,
    string Pedidos,
    IReadOnlyList<string> DocumentosRelevantes,
    IReadOnlyList<string> Riscos,
    IReadOnlyList<string> Lacunas,
    string Metodo);
