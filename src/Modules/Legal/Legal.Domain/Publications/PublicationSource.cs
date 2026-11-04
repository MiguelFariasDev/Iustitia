namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Origem da publicação. Hoje só o DJEN (Diário de Justiça Eletrônico Nacional, via API
/// do CNJ); outros diários/integrações entram como novos valores, sem migração de dados —
/// a coluna é persistida como texto (ver PublicationConfiguration).
/// </summary>
public enum PublicationSource
{
    DJEN,
}
