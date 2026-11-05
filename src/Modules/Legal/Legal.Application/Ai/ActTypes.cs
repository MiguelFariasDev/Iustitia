namespace Advocacia.Modules.Legal.Application.Ai;

/// <summary>
/// Lista FECHADA de tipos de ato que o prompt de classificação pode devolver. Mantida aqui
/// e no classification.md: o validador rejeita qualquer valor fora desta lista, então um
/// act_type inventado pelo modelo nunca chega ao banco. Ao acrescentar um tipo, atualizar o
/// prompt (e a versão dele) no mesmo commit.
/// </summary>
public static class ActTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "despacho",
        "decisao_interlocutoria",
        "sentenca",
        "acordao",
        "decisao_monocratica",
        "intimacao",
        "citacao",
        "audiencia",
        "edital",
        "outros",
    };
}
