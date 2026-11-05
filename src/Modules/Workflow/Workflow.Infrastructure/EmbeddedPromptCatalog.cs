using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;

namespace Advocacia.Modules.Workflow.Infrastructure;

/// <summary>
/// Lê os prompts versionados de Prompts/*.md, embarcados no assembly. Formato do arquivo:
/// front matter com <c>version:</c>, comentários HTML (ignorados) e as seções
/// <c># SYSTEM</c> e <c># USER</c>. Parseado uma vez por prompt e guardado em memória.
/// </summary>
public sealed partial class EmbeddedPromptCatalog : IPromptCatalog
{
    private static readonly Assembly ResourceAssembly = typeof(EmbeddedPromptCatalog).Assembly;

    private readonly ConcurrentDictionary<string, PromptTemplate> _cache = new(StringComparer.Ordinal);

    public Result<PromptTemplate> Get(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
        {
            return Result.Success(cached);
        }

        var parsed = Load(name);
        if (parsed is null)
        {
            return Result.Failure<PromptTemplate>(ErrorFactory.From(ErrorCode.AI_PROMPT_NOT_FOUND, name));
        }

        _cache[name] = parsed;
        return Result.Success(parsed);
    }

    private static PromptTemplate? Load(string name)
    {
        var resourceName = $"{typeof(EmbeddedPromptCatalog).Namespace}.Prompts.{name}.md";

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(name, reader.ReadToEnd());
    }

    /// <summary>Exposto para teste: transforma o texto de um .md no template.</summary>
    internal static PromptTemplate? Parse(string name, string markdown)
    {
        var withoutComments = HtmlCommentRegex().Replace(markdown, string.Empty);

        string? version = null;
        var system = new StringBuilder();
        var user = new StringBuilder();
        StringBuilder? current = null;
        var inFrontMatter = false;
        var frontMatterSeen = false;

        foreach (var rawLine in withoutComments.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line == "---" && !frontMatterSeen)
            {
                inFrontMatter = !inFrontMatter;
                frontMatterSeen = !inFrontMatter;
                continue;
            }

            if (inFrontMatter)
            {
                if (line.StartsWith("version:", StringComparison.Ordinal))
                {
                    version = line["version:".Length..].Trim();
                }

                continue;
            }

            if (line == "# SYSTEM")
            {
                current = system;
                continue;
            }

            if (line == "# USER")
            {
                current = user;
                continue;
            }

            current?.AppendLine(line);
        }

        var systemPrompt = system.ToString().Trim();
        var userTemplate = user.ToString().Trim();

        return string.IsNullOrWhiteSpace(version) || systemPrompt.Length == 0 || userTemplate.Length == 0
            ? null
            : new PromptTemplate(name, version, systemPrompt, userTemplate);
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();
}
