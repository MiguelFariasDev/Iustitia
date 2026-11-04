namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Natureza do ato publicado. Sem acentos nos identificadores (convenção de C#); o rótulo
/// exibido ao usuário fica no frontend. É nullable na <see cref="Publication"/>: na captura
/// (Etapa 1.2) o tipo vem só do que o CNJ informa e pode não ser classificável — a
/// classificação assistida por IA, sempre com revisão humana, é da Fase 3.
/// </summary>
public enum PublicationType
{
    Decisao,
    Despacho,
    Intimacao,
    Sentenca,
    Acordao,
    Outros,
}
