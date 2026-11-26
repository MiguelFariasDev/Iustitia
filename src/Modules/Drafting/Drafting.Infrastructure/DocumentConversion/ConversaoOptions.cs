namespace Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;

/// <summary>Seção <c>Drafting:Conversao</c>: ferramentas locais do worker. Ausente = o formato é recusado com aviso claro.</summary>
public sealed class ConversaoOptions
{
    public const string SectionName = "Drafting:Conversao";

    /// <summary>LibreOffice, para DOC, XLS e ODS.</summary>
    public string LibreOffice { get; set; } = "soffice";

    /// <summary>Poppler, para renderizar páginas escaneadas. Sem ele, usa-se a imagem embutida na página.</summary>
    public string Pdftoppm { get; set; } = "pdftoppm";

    public int TimeoutSegundos { get; set; } = 120;
}

/// <summary>Seção <c>Drafting:Ocr</c>.</summary>
public sealed class OcrOptions
{
    public const string SectionName = "Drafting:Ocr";

    public string Executavel { get; set; } = "tesseract";

    public string Idioma { get; set; } = "por";

    public int Dpi { get; set; } = 300;

    public int TimeoutSegundos { get; set; } = 120;

    /// <summary>Páginas reconhecidas ao mesmo tempo.</summary>
    public int Paralelismo { get; set; } = 2;
}

/// <summary>
/// Seção <c>Drafting:Transcricao</c>: servidor Whisper PRÓPRIO (API compatível com /v1/audio/transcriptions).
/// Vazio = áudio é recusado. Não aponte para serviço externo sem base legal: é voz de cliente.
/// </summary>
public sealed class TranscricaoOptions
{
    public const string SectionName = "Drafting:Transcricao";

    public string BaseUrl { get; set; } = string.Empty;

    public string Caminho { get; set; } = "v1/audio/transcriptions";

    public string Modelo { get; set; } = "whisper-large-v3";

    public string Idioma { get; set; } = "pt";

    public string? ApiKey { get; set; }

    public int TimeoutSegundos { get; set; } = 900;

    public bool AllowInsecureHttp { get; set; }
}
