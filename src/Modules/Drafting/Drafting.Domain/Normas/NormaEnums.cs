namespace Advocacia.Modules.Drafting.Domain.Normas;

public enum NormaStatus
{
    /// <summary>Registrada (ou alterada) e ainda sem chunks/embeddings atuais.</summary>
    Pendente = 0,
    Indexada = 1,
    Falhou = 2,

    /// <summary>O arquivo sumiu do repositório: some da busca, mas o histórico permanece para auditoria.</summary>
    Removida = 3,
}

public enum ChunkStatus
{
    Pendente = 0,
    Indexado = 1,
    Falhou = 2,
    Removido = 3,
}

public enum IngestaoJobTipo
{
    Completa = 0,
    Incremental = 1,
    Reprocessamento = 2,
}

public enum IngestaoJobStatus
{
    Pendente = 0,
    EmAndamento = 1,
    Concluido = 2,
    ConcluidoComErros = 3,
    Falhou = 4,
    Cancelado = 5,
}
