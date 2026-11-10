namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Valores aceitos em <c>tipoPeca</c> e <c>area</c>. O frontend nunca manda texto livre aí.</summary>
public static class PecaCatalogo
{
    public static readonly IReadOnlyDictionary<string, string> Tipos = new Dictionary<string, string>
    {
        ["peticao_inicial"] = "petição inicial",
        ["contestacao"] = "contestação",
        ["replica"] = "réplica",
        ["recurso_apelacao"] = "recurso de apelação",
        ["embargos_declaracao"] = "embargos de declaração",
        ["agravo_instrumento"] = "agravo de instrumento",
        ["contrarrazoes"] = "contrarrazões",
        ["manifestacao"] = "manifestação",
    };

    public static readonly IReadOnlyDictionary<string, string> Areas = new Dictionary<string, string>
    {
        ["civel"] = "cível",
        ["trabalhista"] = "trabalhista",
        ["previdenciario"] = "previdenciário",
        ["tributario"] = "tributário",
        ["familia"] = "família",
        ["consumidor"] = "consumidor",
        ["administrativo"] = "administrativo",
        ["empresarial"] = "empresarial",
    };

    public static string RotuloTipo(string chave) => Tipos.TryGetValue(chave, out var rotulo) ? rotulo : chave;

    public static string RotuloArea(string chave) => Areas.TryGetValue(chave, out var rotulo) ? rotulo : chave;
}
