namespace Advocacia.BuildingBlocks.Application.Messaging;

/// <summary>
/// Marca um command/query como ação a ser registrada na trilha de auditoria imutável
/// (audit_logs) quando concluir COM SUCESSO. Declarativo de propósito: o módulo diz "o quê", o
/// <c>AuditBehavior</c> (Platform.Application) cuida do "como", e Legal/Workflow não precisam
/// depender de Platform para auditar (mesma razão de <c>IAlertPublisher</c> viver aqui).
///
/// O que é gravado: ação, tipo e id da entidade, quem, quando, IP e User-Agent. Valores de campos
/// (CPF, e-mail, observações) NUNCA são gravados — a auditoria registra QUE alguém fez algo com a
/// entidade X, não uma cópia do dado pessoal (que ficaria sem criptografia numa tabela imutável
/// de retenção de 5 anos). A exceção são as propriedades listadas em <see cref="DetailProperties"/>,
/// que devem ser sempre não sensíveis (ex.: o novo status).
/// </summary>
/// <param name="action">Nome de um valor de <c>AuditAction</c> (Created, Updated, Viewed, ...).</param>
/// <param name="entityType">Tipo da entidade afetada (ex.: "Cliente").</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditedAttribute(string action, string entityType) : Attribute
{
    public string Action { get; } = action;

    public string EntityType { get; } = entityType;

    /// <summary>
    /// Propriedade do REQUEST com o id (Guid) da entidade, para ações sobre algo que já existe
    /// (atualizar, desativar, visualizar). Em criações fica vazia e o id vem da propriedade
    /// <c>Id</c> do valor de sucesso da resposta.
    /// </summary>
    public string? EntityIdProperty { get; init; }

    /// <summary>
    /// Quando definida, o valor (string) desta propriedade do request, se for um nome de
    /// <c>AuditAction</c> válido, substitui <see cref="Action"/> — ex.: a revisão de uma publicação
    /// vira Approved/Rejected/Corrected conforme o status escolhido.
    /// </summary>
    public string? ActionFromProperty { get; init; }

    /// <summary>Propriedades do request gravadas em <c>after</c>. Só dados NÃO sensíveis.</summary>
    public string[] DetailProperties { get; init; } = [];
}
