namespace Advocacia.Modules.Legal.Domain.Ai;

/// <summary>Desfecho de uma chamada ao provedor de IA, para auditoria e custo.</summary>
public enum AiCallStatus
{
    /// <summary>Chamada feita e resposta validada.</summary>
    Succeeded,

    /// <summary>Chamada feita (e COBRADA), mas a resposta não passou na validação do contrato.</summary>
    InvalidResponse,

    /// <summary>A chamada não chegou a produzir resposta (rede, auth, limite, timeout).</summary>
    Failed,
}
