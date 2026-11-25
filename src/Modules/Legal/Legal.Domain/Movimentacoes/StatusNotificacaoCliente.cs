namespace Advocacia.Modules.Legal.Domain.Movimentacoes;

/// <summary>Persistido como texto (nome do membro): o nome não deve mudar.</summary>
public enum StatusNotificacaoCliente
{
    /// <summary>Rascunho criado pelo acompanhamento, esperando o advogado. NADA sai daqui sozinho.</summary>
    PendenteRevisao,

    /// <summary>Revisado e enviado pelo advogado.</summary>
    Enviada,

    /// <summary>O advogado decidiu não avisar.</summary>
    Descartada,

    /// <summary>Revisado, mas o provedor de e-mail falhou; pode ser reenviado.</summary>
    FalhaEnvio,
}
