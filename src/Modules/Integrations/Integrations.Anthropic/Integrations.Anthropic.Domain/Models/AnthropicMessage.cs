namespace Advocacia.Modules.Integrations.Anthropic.Domain.Models;

/// <summary>
/// Uma mensagem da conversa. <see cref="Role"/> é "user" ou "assistant". O conteúdo é SEMPRE
/// tratado como dado sigiloso: nunca vai para log, métrica, span ou erro.
///
/// Nesta etapa o conteúdo é texto. Blocos estruturados (tool_result, imagens) chegam com a
/// Athena (Fase 5), que vai estender este tipo.
/// </summary>
public sealed record AnthropicMessage(string Role, string Content)
{
    public const string UserRole = "user";

    public const string AssistantRole = "assistant";

    public static AnthropicMessage User(string content) => new(UserRole, content);

    public static AnthropicMessage Assistant(string content) => new(AssistantRole, content);
}
