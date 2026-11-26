using System.Globalization;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;

/// <summary>DOCX → Markdown: títulos, listas, tabelas e parágrafos. Texto excluído em controle de alterações não entra.</summary>
public sealed class DocxConversor : IConversorFormato
{
    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Docx];

    public Task<Result<DocumentoConvertido>> ConverterAsync(DocumentoParaConverter documento, CancellationToken cancellationToken = default) =>
        Task.FromResult(Converter(documento.Conteudo));

    internal static Result<DocumentoConvertido> Converter(byte[] conteudo)
    {
        if (!Apoio.ZipSeguro(conteudo))
        {
            return Apoio.NaoSuportado();
        }

        try
        {
            using var stream = new MemoryStream(conteudo, writable: false);
            using var docx = WordprocessingDocument.Open(stream, false);
            if (docx.MainDocumentPart?.Document?.Body is not { } corpo)
            {
                return Apoio.NaoSuportado();
            }

            var niveis = NiveisDeTitulo(docx.MainDocumentPart);
            var sb = new StringBuilder();
            var alteracoes = corpo.Descendants<DeletedRun>().Any() || corpo.Descendants<InsertedRun>().Any();
            foreach (var elemento in corpo.Elements())
            {
                switch (elemento)
                {
                    case Paragraph p:
                        sb.Append(Paragrafo(p, niveis));
                        break;
                    case Table t:
                        sb.Append('\n').Append(Apoio.Tabela(t.Elements<TableRow>()
                            .Select(r => (IReadOnlyList<string>)r.Elements<TableCell>().Select(c => Texto(c, " ")).ToList()).ToList())).Append('\n');
                        break;
                    case SdtBlock bloco:
                        foreach (var p in bloco.Descendants<Paragraph>())
                        {
                            sb.Append(Paragrafo(p, niveis));
                        }

                        break;
                }
            }

            var titulo = docx.PackageProperties.Title;
            var avisos = alteracoes
                ? new List<string> { "O arquivo tem controle de alterações: foi considerado o texto com as alterações aceitas." }
                : [];

            return Apoio.Pronto(
                string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim(),
                [new BlocoConvertido(null, null, sb.ToString(), OrigemTexto.Nativo)], null, avisos);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or IOException or FormatException
                                       or ArgumentException or System.Xml.XmlException or InvalidOperationException)
        {
            return Apoio.NaoSuportado();
        }
    }

    private static string Paragrafo(Paragraph p, IReadOnlyDictionary<string, int> niveis)
    {
        var texto = Texto(p, "\n").Trim();
        if (texto.Length == 0)
        {
            return "\n";
        }

        var estilo = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var nivel = p.ParagraphProperties?.OutlineLevel?.Val?.Value is { } direto && direto < 6
            ? direto + 1
            : estilo is not null && niveis.TryGetValue(estilo, out var doEstilo) ? doEstilo : 0;
        if (nivel > 0)
        {
            return $"\n{new string('#', Math.Min(nivel + 1, 6))} {texto.ReplaceLineEndings(" ")}\n\n";
        }

        if (p.ParagraphProperties?.NumberingProperties is { } numeracao)
        {
            return $"{new string(' ', 2 * Math.Clamp(numeracao.NumberingLevelReference?.Val?.Value ?? 0, 0, 8))}- {texto}\n";
        }

        return texto + "\n\n";
    }

    /// <summary>Só texto visível: ignora o que está em <c>w:del</c>, códigos de campo e propriedades.</summary>
    private static string Texto(DocumentFormat.OpenXml.OpenXmlElement raiz, string quebra)
    {
        var sb = new StringBuilder();
        foreach (var e in raiz.Descendants())
        {
            switch (e)
            {
                case Text t:
                    sb.Append(t.Text);
                    break;
                case TabChar:
                    sb.Append(' ');
                    break;
                case Break or CarriageReturn:
                    sb.Append(quebra);
                    break;
                case Paragraph when sb.Length > 0 && raiz is not Paragraph:
                    sb.Append(quebra);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Estilo → nível de título, inclusive em Word em português ("Ttulo1") e estilos com nível de tópico.</summary>
    private static Dictionary<string, int> NiveisDeTitulo(MainDocumentPart parte)
    {
        var niveis = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 6; i++)
        {
            niveis["Heading" + i] = i;
            niveis["Ttulo" + i] = i;
        }

        niveis["Title"] = 1;
        foreach (var estilo in parte.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
        {
            if (estilo.StyleId?.Value is { } id && estilo.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } nivel && nivel < 6)
            {
                niveis[id] = nivel + 1;
            }
        }

        return niveis;
    }
}

/// <summary>Formatos antigos (DOC, XLS, ODS) passam pelo LibreOffice em modo headless, com perfil descartável.</summary>
public sealed class LibreOfficeConversor(IOptions<ConversaoOptions> options)
{
    // O LibreOffice não convive bem com execuções simultâneas, mesmo com perfis separados.
    private static readonly SemaphoreSlim Trava = new(1, 1);

    /// <summary>Nulo = LibreOffice ausente ou arquivo que ele não conseguiu abrir.</summary>
    public async Task<(bool Instalado, byte[]? Conteudo)> ConverterAsync(
        byte[] conteudo, string extensaoOrigem, string formatoDestino, CancellationToken cancellationToken)
    {
        await Trava.WaitAsync(cancellationToken);
        try
        {
            return await ProcessoExterno.EmDiretorioTemporarioAsync(async diretorio =>
            {
                var entrada = Path.Combine(diretorio, "entrada" + extensaoOrigem);
                var saida = Path.Combine(diretorio, "saida");
                await File.WriteAllBytesAsync(entrada, conteudo, cancellationToken);
                Directory.CreateDirectory(saida);

                var execucao = await ProcessoExterno.ExecutarAsync(
                    options.Value.LibreOffice,
                    [
                        $"-env:UserInstallation=file://{diretorio}/perfil", "--headless", "--norestore", "--nolockcheck", "--nodefault",
                        "--convert-to", formatoDestino, "--outdir", saida, entrada,
                    ],
                    null, TimeSpan.FromSeconds(options.Value.TimeoutSegundos), cancellationToken);
                var arquivo = Path.Combine(saida, "entrada." + formatoDestino);

                return (execucao is not null, File.Exists(arquivo) ? await File.ReadAllBytesAsync(arquivo, cancellationToken) : null);
            });
        }
        finally
        {
            Trava.Release();
        }
    }
}

public sealed class DocConversor(LibreOfficeConversor libreOffice) : IConversorFormato
{
    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Doc];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var (instalado, docx) = await libreOffice.ConverterAsync(documento.Conteudo, ".doc", "docx", cancellationToken);
        if (!instalado)
        {
            return Apoio.Indisponivel("DOC antigo (LibreOffice não instalado). Salve como DOCX ou PDF");
        }

        return docx is null ? Apoio.NaoSuportado() : DocxConversor.Converter(docx);
    }
}

/// <summary>Planilhas → uma tabela Markdown por aba, só com linhas preenchidas, até o limite configurado.</summary>
public sealed class PlanilhaConversor(LibreOfficeConversor libreOffice, IOptions<AnexosOptions> options) : IConversorFormato
{
    private const int MaxColunas = 40;

    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Planilha];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var extensao = Path.GetExtension(documento.NomeArquivo).ToLowerInvariant();
        var conteudo = documento.Conteudo;
        if (extensao == ".csv")
        {
            return Csv(Apoio.Decodificar(conteudo));
        }

        if (extensao is ".xls" or ".ods")
        {
            var (instalado, xlsx) = await libreOffice.ConverterAsync(conteudo, extensao, "xlsx", cancellationToken);
            if (!instalado)
            {
                return Apoio.Indisponivel("planilha XLS/ODS (LibreOffice não instalado). Salve como XLSX ou CSV");
            }

            if (xlsx is null)
            {
                return Apoio.NaoSuportado();
            }

            conteudo = xlsx;
        }

        return Xlsx(conteudo);
    }

    private Result<DocumentoConvertido> Xlsx(byte[] conteudo)
    {
        if (!Apoio.ZipSeguro(conteudo))
        {
            return Apoio.NaoSuportado();
        }

        try
        {
            using var stream = new MemoryStream(conteudo, writable: false);
            using var planilha = SpreadsheetDocument.Open(stream, false);
            if (planilha.WorkbookPart is not { } livro)
            {
                return Apoio.NaoSuportado();
            }

            var textos = livro.SharedStringTablePart?.SharedStringTable?.Elements<S.SharedStringItem>().Select(i => i.InnerText).ToArray() ?? [];
            var datas = EstilosDeData(livro);
            var blocos = new List<BlocoConvertido>();
            var avisos = new List<string>();
            foreach (var aba in livro.Workbook?.Descendants<S.Sheet>() ?? [])
            {
                if (aba.Id?.Value is not { } id || livro.GetPartById(id) is not WorksheetPart parte)
                {
                    continue;
                }

                var linhas = new List<IReadOnlyList<string>>();
                var cortada = false;
                foreach (var linha in parte.Worksheet?.Descendants<S.Row>() ?? [])
                {
                    var celulas = Celulas(linha, textos, datas);
                    if (celulas.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    if (linhas.Count >= options.Value.MaxLinhasPlanilha)
                    {
                        cortada = true;
                        break;
                    }

                    linhas.Add(celulas);
                }

                if (linhas.Count == 0)
                {
                    continue;
                }

                var nome = aba.Name?.Value ?? "Planilha";
                if (cortada)
                {
                    avisos.Add($"A aba '{nome}' passou de {options.Value.MaxLinhasPlanilha} linhas; o restante não foi lido.");
                }

                blocos.Add(new BlocoConvertido(null, nome, $"## {nome}\n\n{Apoio.Tabela(linhas)}", OrigemTexto.Nativo));
            }

            return blocos.Count == 0 ? Apoio.NaoSuportado() : Apoio.Pronto(null, blocos, null, avisos);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or IOException or FormatException
                                       or ArgumentException or System.Xml.XmlException or InvalidOperationException)
        {
            return Apoio.NaoSuportado();
        }
    }

    private static List<string> Celulas(S.Row linha, string[] textos, HashSet<uint> datas)
    {
        var valores = new List<string>();
        foreach (var celula in linha.Elements<S.Cell>())
        {
            var coluna = Coluna(celula.CellReference?.Value) ?? valores.Count;
            if (coluna >= MaxColunas)
            {
                break;
            }

            // O XLSX omite células vazias: sem preencher, a linha desalinharia do cabeçalho.
            while (valores.Count < coluna)
            {
                valores.Add(string.Empty);
            }

            valores.Add(Valor(celula, textos, datas));
        }

        return valores;
    }

    private static string Valor(S.Cell celula, string[] textos, HashSet<uint> datas)
    {
        var bruto = celula.CellValue?.Text ?? string.Empty;
        var tipo = celula.DataType?.Value;
        if (tipo == S.CellValues.SharedString)
        {
            return int.TryParse(bruto, out var i) && i >= 0 && i < textos.Length ? textos[i] : string.Empty;
        }

        if (tipo == S.CellValues.InlineString)
        {
            return celula.InlineString?.InnerText ?? string.Empty;
        }

        if (tipo == S.CellValues.Boolean)
        {
            return bruto == "1" ? "sim" : "não";
        }

        if ((tipo is null || tipo == S.CellValues.Number)
            && celula.StyleIndex?.Value is { } estilo && datas.Contains(estilo)
            && double.TryParse(bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 0 and < 2_958_466)
        {
            var data = DateTime.FromOADate(serial);

            return data.TimeOfDay == TimeSpan.Zero
                ? data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : data.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        // 5000.5 → 5000,5: o mesmo formato decimal que aparece no texto das peças.
        return double.TryParse(bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) && tipo is null
            ? numero.ToString("0.##########", CultureInfo.InvariantCulture).Replace('.', ',')
            : bruto;
    }

    private static HashSet<uint> EstilosDeData(WorkbookPart livro)
    {
        var folha = livro.WorkbookStylesPart?.Stylesheet;
        var personalizados = folha?.NumberingFormats?.Elements<S.NumberingFormat>()
            .Where(f => f.FormatCode?.Value is { } c && !c.Contains("0.", StringComparison.Ordinal)
                && (c.Contains("yy", StringComparison.OrdinalIgnoreCase) || c.Contains("dd", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("aa", StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.NumberFormatId?.Value ?? 0).ToHashSet() ?? [];

        return folha?.CellFormats?.Elements<S.CellFormat>()
            .Select((f, i) => (Formato: f.NumberFormatId?.Value ?? 0, Indice: (uint)i))
            .Where(f => f.Formato is (>= 14 and <= 17) or 22 || personalizados.Contains(f.Formato))
            .Select(f => f.Indice).ToHashSet() ?? [];
    }

    /// <summary>"C7" → 2.</summary>
    private static int? Coluna(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia))
        {
            return null;
        }

        var coluna = 0;
        foreach (var c in referencia.TakeWhile(char.IsAsciiLetter))
        {
            coluna = (coluna * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return coluna == 0 ? null : coluna - 1;
    }

    private Result<DocumentoConvertido> Csv(string texto)
    {
        var primeira = texto.Split('\n', 2)[0];
        var separador = new[] { ';', '\t', ',' }.OrderByDescending(s => primeira.Count(c => c == s)).First();
        var linhas = new List<IReadOnlyList<string>>();
        var celula = new StringBuilder();
        var atual = new List<string>();
        var aspas = false;
        var cortado = false;

        bool FecharLinha()
        {
            atual.Add(celula.ToString());
            celula.Clear();
            if (atual.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                linhas.Add(atual.Take(MaxColunas).ToList());
            }

            atual = [];

            return linhas.Count < options.Value.MaxLinhasPlanilha;
        }

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (c == '"')
            {
                if (aspas && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    celula.Append('"');
                    i++;
                }
                else
                {
                    aspas = !aspas;
                }
            }
            else if (c == separador && !aspas)
            {
                atual.Add(celula.ToString());
                celula.Clear();
            }
            else if (c == '\n' && !aspas)
            {
                if (!FecharLinha())
                {
                    cortado = i + 1 < texto.Length;
                    break;
                }
            }
            else if (c != '\r')
            {
                celula.Append(c);
            }
        }

        if (!cortado && (celula.Length > 0 || atual.Count > 0))
        {
            FecharLinha();
        }

        return linhas.Count == 0
            ? Apoio.NaoSuportado()
            : Apoio.Pronto(
                null, [new BlocoConvertido(null, null, Apoio.Tabela(linhas), OrigemTexto.Nativo)], null,
                cortado ? [$"A planilha passou de {options.Value.MaxLinhasPlanilha} linhas; o restante não foi lido."] : []);
    }
}
