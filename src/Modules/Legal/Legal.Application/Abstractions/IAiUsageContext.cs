namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Contexto da execução corrente (escopo do job) que liga as chamadas de IA à publicação
/// processada. Existe porque a assinatura dos serviços de IA não recebe o id da publicação
/// — o conteúdo é o que importa para o modelo, e o id não deve vazar para o prompt.
/// </summary>
public interface IAiUsageContext
{
    /// <summary>Publicação sendo processada; nulo fora de um job.</summary>
    Guid? PublicationId { get; set; }
}
