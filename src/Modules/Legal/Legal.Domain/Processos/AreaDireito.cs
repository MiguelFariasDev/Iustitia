namespace Advocacia.Modules.Legal.Domain.Processos;

/// <summary>
/// Área do direito do processo. Enum e não texto livre porque é campo de filtro e de
/// relatório — texto livre viraria "Cível", "civel", "Civil" na mesma base em uma semana.
/// </summary>
public enum AreaDireito
{
    Civel,
    Trabalhista,
    Criminal,
    Tributario,
    Previdenciario,
    Familia,
    Consumidor,
    Empresarial,
    Administrativo,
    Ambiental,
    Outros,
}
