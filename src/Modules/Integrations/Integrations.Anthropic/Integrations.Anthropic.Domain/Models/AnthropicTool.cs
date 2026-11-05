namespace Advocacia.Modules.Integrations.Anthropic.Domain.Models;

/// <summary>
/// Ferramenta (function calling) oferecida ao modelo. <see cref="InputSchema"/> é um JSON
/// Schema como texto (ex.: <c>{"type":"object","properties":{...}}</c>). As ferramentas
/// concretas do sistema (consultar processo etc.) são da Fase 5 (Athena).
/// </summary>
public sealed record AnthropicTool(string Name, string Description, string InputSchema);

/// <summary>
/// Pedido do modelo para que o sistema execute uma ferramenta (bloco <c>tool_use</c>).
/// <see cref="InputJson"/> são os argumentos, como JSON — a validação contra o schema e a
/// decisão de executar são de quem consome.
/// </summary>
public sealed record AnthropicToolUse(string Id, string Name, string InputJson);
