namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

/// <summary>
/// Seção "Juscraper" do appsettings. A <see cref="ApiKey"/> vai em user-secrets (dev) ou no Key
/// Vault (produção) — nunca no appsettings versionado.
/// </summary>
public sealed class JuscraperOptions
{
    public const string SectionName = "Juscraper";

    /// <summary>
    /// Endereço do serviço (ex.: http://juscraper:8080/). VAZIO desliga o juscraper: a consulta
    /// volta a usar só o DataJud e o acompanhamento só o DataJud — o comportamento anterior.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Valor do header X-Api-Key (uma das chaves de Juscraper__Api__ApiKeys do serviço).</summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Por tentativa, na consulta do cadastro. Há uma pessoa esperando; com max_movimentacoes=10 o
    /// TJSP responde em 2 a 4 s, mas uma consulta fria de processo grande passa de 10 s.
    /// </summary>
    public TimeSpan ConsultaTimeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Por tentativa, no acompanhamento: a primeira sincronização traz o histórico inteiro.</summary>
    public TimeSpan AcompanhamentoTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Usar o DataJud quando o juscraper falhar ou não cobrir o tribunal. O Termo de Uso do DataJud
    /// proíbe uso comercial (ADR-064): no dia em que o produto for comercializado, este fallback
    /// precisa ser desligado (false) ou trocado por um provedor com contrato.
    /// </summary>
    public bool FallbackDataJud { get; init; } = true;

    /// <summary>
    /// Consultar o DataJud também quando o portal responde "não encontrado". Desligado por padrão:
    /// o portal é a fonte primária e mais atual, e um "não existe" dele é confiável.
    /// </summary>
    public bool FallbackQuandoNaoEncontrado { get; init; }

    public bool Habilitado => !string.IsNullOrWhiteSpace(BaseUrl);
}
