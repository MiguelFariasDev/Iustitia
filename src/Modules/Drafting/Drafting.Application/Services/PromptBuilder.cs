using System.Text;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Monta os prompts de forma DETERMINÍSTICA: mesma entrada, mesmos bytes. O system prompt é fixo
/// (<see cref="SystemPrompt"/>); texto do usuário e trechos recuperados só entram no user prompt.
/// </summary>
public sealed class PromptBuilder : IPromptBuilder
{
    public const string SemInstrucoes = "Nenhuma.";
    public const string SemPedidos = "Não informados.";
    public const string SemContexto = "(nenhum trecho relevante foi recuperado)";

    public PromptBundle Build(PecaInput input, IReadOnlyList<RetrievedChunk> chunks)
    {
        var tipo = PecaCatalogo.RotuloTipo(input.TipoPeca);
        var area = PecaCatalogo.RotuloArea(input.Area);

        var user = new StringBuilder();
        user.Append("TIPO DE PEÇA:\n").Append(tipo).Append("\n\n");
        user.Append("ÁREA:\n").Append(area).Append("\n\n");
        user.Append("FATOS:\n").Append(input.Fatos).Append("\n\n");
        user.Append("PEDIDOS:\n").Append(string.IsNullOrWhiteSpace(input.Pedidos) ? SemPedidos : input.Pedidos).Append("\n\n");
        user.Append("INSTRUÇÕES ADICIONAIS:\n")
            .Append(string.IsNullOrWhiteSpace(input.InstrucoesAdicionais) ? SemInstrucoes : input.InstrucoesAdicionais)
            .Append("\n\n");
        user.Append("CONTEXTO RECUPERADO:\n");

        if (chunks.Count == 0)
        {
            user.Append(SemContexto).Append("\n\n");
        }
        else
        {
            foreach (var chunk in chunks)
            {
                user.Append('[').Append(chunk.Rotulo).Append("]\n");
                if (chunk.Meta.Norma is { } norma)
                {
                    // Vigência e versão vão no contexto para o modelo cumprir as regras 14 e 15.
                    user.Append("(Situação: ").Append(norma.Revogada ? "REVOGADA" : norma.Situacao)
                        .Append(" · Texto: ").Append(norma.VersaoTexto);
                    if (!string.IsNullOrWhiteSpace(chunk.Meta.UrlOrigem))
                    {
                        user.Append(" · Fonte: ").Append(chunk.Meta.UrlOrigem);
                    }

                    user.Append(")\n");
                }

                user.Append(chunk.Texto.Trim())
                    .Append("\n\n");
            }
        }

        user.Append("TAREFA:\n")
            .Append("Redija uma minuta de ").Append(tipo)
            .Append(" com base exclusivamente nas informações acima. Não invente fatos, provas ou fundamentos jurídicos. ")
            .Append("Destaque expressamente as informações ausentes.");

        return new PromptBundle(SystemPrompt.Texto, user.ToString(), SystemPrompt.Versao);
    }
}
