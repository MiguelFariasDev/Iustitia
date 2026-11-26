using System.Text;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Monta os prompts de forma DETERMINÍSTICA: mesma entrada, mesmos bytes. O system prompt é fixo
/// (<see cref="SystemPrompt"/>); texto da advogada e trechos recuperados só entram no user prompt, e todo
/// texto que veio de fora (anexo ou acervo) tem os rótulos de seção neutralizados — um anexo não consegue
/// abrir uma falsa seção "TAREFA:".
/// </summary>
public sealed class PromptBuilder : IPromptBuilder
{
    public const string SemInstrucoes = "Nenhuma.";
    public const string SemPedidos = "Não informados.";
    public const string SemDocumentos = "(nenhum trecho relevante foi encontrado nos documentos do caso)";
    public const string SemContexto = "(nenhum trecho relevante foi recuperado)";

    public PromptBundle Build(PecaInput input, RagContext contexto)
    {
        var tipo = PecaCatalogo.RotuloTipo(input.TipoPeca);
        var user = new StringBuilder();

        Secao(user, "TIPO DE PEÇA", tipo);
        Secao(user, "ÁREA", PecaCatalogo.RotuloArea(input.Area));
        Secao(user, "FATOS FORNECIDOS PELA ADVOGADA", input.Fatos);
        Secao(user, "PEDIDOS SOLICITADOS", string.IsNullOrWhiteSpace(input.Pedidos) ? SemPedidos : input.Pedidos);
        Secao(
            user, "INSTRUÇÕES ADICIONAIS",
            string.IsNullOrWhiteSpace(input.InstrucoesAdicionais) ? SemInstrucoes : input.InstrucoesAdicionais);

        user.Append("DOCUMENTOS DO CASO:\n");
        Trechos(user, contexto.DoGrupo(GrupoContexto.DocumentosDoCaso).ToList(), SemDocumentos);

        user.Append("CONTEXTO JURÍDICO RECUPERADO:\n");
        var temJuridico = false;
        foreach (var (grupo, titulo) in new[]
                 {
                     (GrupoContexto.Constituicao, "Constituição Federal"),
                     (GrupoContexto.LeisFederais, "Leis federais"),
                     (GrupoContexto.Jurisprudencia, "Jurisprudência"),
                 })
        {
            var trechos = contexto.DoGrupo(grupo).ToList();
            if (trechos.Count == 0)
            {
                continue;
            }

            temJuridico = true;
            user.Append("--- ").Append(titulo).Append(" ---\n");
            Trechos(user, trechos, string.Empty);
        }

        if (!temJuridico)
        {
            user.Append(SemContexto).Append("\n\n");
        }

        user.Append("TAREFA:\n")
            .Append("Redija uma minuta de ").Append(tipo).Append(" com base exclusivamente nas informações acima.\n")
            .Append("Não invente fatos, provas ou fundamentos jurídicos.\n")
            .Append("Destaque expressamente as informações ausentes.");

        return new PromptBundle(SystemPrompt.Texto, user.ToString(), SystemPrompt.Versao);
    }

    private static void Secao(StringBuilder user, string titulo, string conteudo) =>
        user.Append(titulo).Append(":\n").Append(conteudo).Append("\n\n");

    private static void Trechos(StringBuilder user, IReadOnlyList<RetrievedChunk> chunks, string vazio)
    {
        if (chunks.Count == 0)
        {
            user.Append(vazio).Append("\n\n");

            return;
        }

        foreach (var chunk in chunks)
        {
            user.Append('[').Append(chunk.Rotulo).Append("]\n");
            if (Contexto(chunk) is { Length: > 0 } linha)
            {
                user.Append('(').Append(linha).Append(")\n");
            }

            user.Append(InputSanitizer.NeutralizarMarcadores(chunk.Texto).Trim()).Append("\n\n");
        }
    }

    /// <summary>Vigência e versão (normas) ou confiabilidade (anexos): o modelo precisa disto para cumprir as regras 7 e 8.</summary>
    private static string Contexto(RetrievedChunk chunk)
    {
        if (chunk.Meta.Norma is { } norma)
        {
            var partes = new List<string>
            {
                $"Situação: {(norma.ForaDeVigor() ? $"FORA DE VIGOR — {norma.Situacao}" : norma.Situacao)}",
                $"Texto: {norma.VersaoTexto}",
            };
            if (chunk.Meta.DataPublicacao is { } data)
            {
                partes.Add($"Data: {data:dd/MM/yyyy}");
            }

            if (!string.IsNullOrWhiteSpace(norma.Urn))
            {
                partes.Add($"URN: {norma.Urn}");
            }

            return string.Join(" · ", partes);
        }

        return chunk.Meta.Caso switch
        {
            { Origem: OrigemTexto.Transcricao } => "Transcrição automática de áudio: não é a prova original; confira a gravação",
            { Origem: OrigemTexto.Ocr, RevisaoPendente: true } => "Texto lido por OCR de baixa confiança, não conferido: sinalize em PONTOS PARA REVISÃO HUMANA",
            { Origem: OrigemTexto.Ocr } => "Texto lido por OCR",
            _ => string.Empty,
        };
    }
}
