using System.Globalization;
using System.Text;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <summary>Markdown normalizado do anexo, com o cabeçalho que preserva origem, página e confiabilidade.</summary>
public static class MarkdownDocumento
{
    public static string Render(DocumentoConvertido convertido, CasoDocumento documento, Caso caso)
    {
        var sb = new StringBuilder();
        Cabecalho(sb, convertido, documento, caso);

        int? pagina = null;
        foreach (var bloco in convertido.Blocos)
        {
            if (bloco.Pagina is { } p && p != pagina)
            {
                pagina = p;
                sb.Append("\n---\n\n**Página:** ").Append(p.ToString(CultureInfo.InvariantCulture)).Append("  \n");
                if (bloco.Origem != OrigemTexto.Nativo)
                {
                    sb.Append("**Confiança ").Append(bloco.Origem == OrigemTexto.Ocr ? "OCR" : "da transcrição").Append(":** ")
                        .Append(RotuloConfianca(bloco.Confianca)).Append("  \n");
                }

                sb.Append('\n');
            }

            sb.Append(bloco.Markdown).Append("\n\n");
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>alta ≥ 0,90 · média ≥ 0,80 · baixa abaixo disso (exige revisão humana).</summary>
    public static string RotuloConfianca(double? confianca) => confianca switch
    {
        null => "não medida",
        >= 0.90 => "alta",
        >= CasoDocumento.ConfiancaOcrBaixa => "média",
        _ => "baixa",
    };

    private static void Cabecalho(StringBuilder sb, DocumentoConvertido convertido, CasoDocumento documento, Caso caso)
    {
        sb.Append("# ").Append(Linha(convertido.Titulo ?? Path.GetFileNameWithoutExtension(documento.NomeArquivo))).Append("\n\n");
        Campo(sb, "Documento", documento.NomeArquivo);
        Campo(sb, "Tipo", documento.TipoDocumento ?? documento.Formato.ToString().ToLowerInvariant());
        Campo(sb, "Caso", caso.Id.ToString());
        Campo(sb, "Cliente", caso.ClienteNome ?? caso.ClienteId.ToString());
        Campo(sb, "Data", documento.DataDocumento?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "não identificada");
        Campo(sb, "Páginas", convertido.Paginas == 0 ? "sem paginação" : convertido.Paginas.ToString(CultureInfo.InvariantCulture));
        Campo(sb, "Origem", convertido.Origem switch
        {
            OrigemTexto.Ocr => "OCR (pode conter erro de leitura)",
            OrigemTexto.Transcricao => "transcrição automática — não é a prova original",
            _ => "texto nativo do arquivo",
        });
        if (convertido.Origem != OrigemTexto.Nativo)
        {
            Campo(sb, "Confiança", RotuloConfianca(convertido.Confianca));
        }

        Campo(sb, "Hash SHA-256", documento.HashSha256);
        Campo(sb, "Responsável pelo upload", documento.UsuarioIdResponsavel.ToString());
        foreach (var (chave, valor) in convertido.Metadados)
        {
            Campo(sb, Linha(chave), valor);
        }

        foreach (var aviso in convertido.Avisos)
        {
            sb.Append("\n> **Aviso:** ").Append(Linha(aviso)).Append('\n');
        }

        sb.Append('\n');
    }

    private static void Campo(StringBuilder sb, string nome, string valor) =>
        sb.Append("**").Append(nome).Append(":** ").Append(Linha(valor)).Append("  \n");

    private static string Linha(string valor) => valor.ReplaceLineEndings(" ").Trim();
}
