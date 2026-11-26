namespace Advocacia.Platform.Application.Common;

/// <summary>
/// Semântica dos campos opcionais de atualização (PUT usado como edição parcial): <c>null</c> = o cliente não
/// mexeu, mantém o valor atual; texto em branco = limpar; qualquer outro texto = gravar aparado. Sem isto,
/// um formulário que não edita logo, avatar ou especialidade os apagaria a cada "Salvar".
/// </summary>
internal static class CampoOpcional
{
    internal static string? Resolver(string? pedido, string? atual) =>
        pedido is null ? atual : string.IsNullOrWhiteSpace(pedido) ? null : pedido.Trim();
}
