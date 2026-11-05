namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>
/// Seção "Anthropic:Models" do appsettings: qual modelo atende cada finalidade. Mora na
/// camada Application (um POCO sem dependências) para que o módulo Legal leia a configuração
/// sem referenciar a infraestrutura da integração.
///
/// Trocar de modelo é mudança de configuração, não de código — mas exige rodar os testes de
/// regressão de prompt (dotnet test --filter Category=PromptRegression) antes de promover.
/// </summary>
public sealed class AnthropicModelsOptions
{
    public const string SectionName = "Anthropic:Models";

    public string Classification { get; init; } = "claude-sonnet-5-5";

    public string Deadline { get; init; } = "claude-sonnet-5-5";

    /// <summary>
    /// Teto de tokens de saída por chamada. Com thinking adaptativo ligado, o raciocínio
    /// conta contra este teto — por isso é folgado frente ao JSON de ~150 tokens esperado.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 4096;

    /// <summary>
    /// Máximo de caracteres da publicação enviados ao modelo. Publicações longas são cortadas
    /// (e a sugestão marca que o corte aconteceu) para limitar custo e o espaço para injeção
    /// de instrução dentro do texto.
    /// </summary>
    public int MaxContentCharacters { get; init; } = 16000;
}
