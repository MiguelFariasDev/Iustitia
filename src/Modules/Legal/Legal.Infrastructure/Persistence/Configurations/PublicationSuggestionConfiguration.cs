namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// TODO (Fase 3 — classificação por IA e sugestão de prazo): configuração Fluent API da
/// tabela <c>publication_suggestions</c>, que guardará a sugestão gerada pelo Claude para
/// uma publicação (tipo do ato, prazo sugerido, justificativa, tokens/custo da chamada) e o
/// resultado da revisão humana obrigatória (ver ADR-012).
///
/// Deliberadamente NÃO implementada agora, e por isso também sem entidade de domínio,
/// DbSet nem tabela na migration desta etapa: a Etapa 1.2 é exclusivamente captura e
/// persistência. Criar a tabela antes de o agregado existir produziria um esquema adivinhado
/// que a Fase 3 teria que migrar de qualquer forma — o custo de adicioná-la junto com a
/// feature é menor que o de corrigir um palpite.
/// </summary>
internal static class PublicationSuggestionConfiguration
{
    /// <summary>Nome reservado da tabela, para manter a convenção documentada em um único lugar.</summary>
    internal const string TableName = "publication_suggestions";
}
