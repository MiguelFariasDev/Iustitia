namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Seção <c>Drafting</c> do appsettings.</summary>
public sealed class DraftingOptions
{
    public const string SectionName = "Drafting";

    /// <summary>
    /// Desligado por padrão: ligar significa mandar fatos do caso (dado sigiloso de cliente) para a
    /// instância de IA configurada. Só habilite depois do checklist da ADR-072.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>Seção <c>Drafting:Rag</c>. <see cref="LimiteResultados"/> vem de <c>Qdrant:LimiteResultados</c>.</summary>
public sealed class RagOptions
{
    public const string SectionName = "Drafting:Rag";

    public const int MinResultados = 5;
    public const int MaxResultados = 10;

    /// <summary>Trechos no contexto. Fica entre 5 e 10 mesmo que a configuração diga outra coisa.</summary>
    public int LimiteResultados { get; set; } = 8;

    /// <summary>Teto do contexto recuperado, em tokens APROXIMADOS (ver <see cref="CharsPorToken"/>).</summary>
    public int MaxContextTokens { get; set; } = 8000;

    /// <summary>
    /// Estimativa de caracteres por token em português. Aproximação deliberadamente conservadora:
    /// o tokenizador real do modelo não está disponível aqui, e errar para menos estouraria o contexto.
    /// </summary>
    public double CharsPorToken { get; set; } = 3.5;

    /// <summary>Corta trechos com similaridade menor que isto. Nulo = só o limite de quantidade.</summary>
    public double? ScoreMinimo { get; set; }

    public int LimiteEfetivo => Math.Clamp(LimiteResultados, MinResultados, MaxResultados);
}
