using System.Reflection;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Modules.Financeiro.Application;
using Advocacia.Modules.Legal.Application;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using MediatR;

namespace Advocacia.ArchitectureTests;

/// <summary>
/// O <c>AuditBehavior</c> engole qualquer falha ao gravar a auditoria (só registra em log), então um
/// <c>[Audited]</c> com ação inexistente ou sem como achar o id da entidade NÃO quebra nada — apenas deixa
/// a operação sem trilha, em silêncio. Estes testes transformam esse erro silencioso em falha de build.
/// </summary>
public class AuditedConventionTests
{
    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(CreateTenantCommand).Assembly,
        typeof(LegalApplicationAssembly).Assembly,
        typeof(FinanceiroApplicationAssembly).Assembly,
    ];

    private static IEnumerable<(Type Request, AuditedAttribute Audit)> AuditedRequests() =>
        ApplicationAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => (Type: type, Audit: type.GetCustomAttribute<AuditedAttribute>()))
            .Where(item => item.Audit is not null)
            .Select(item => (item.Type, item.Audit!));

    [Fact]
    public void AuditedRequests_ExistInTheScannedAssemblies()
    {
        AuditedRequests().Should().NotBeEmpty("se isto falhar, o scanner deixou de achar os atributos e os testes abaixo passariam em vão");
    }

    [Fact]
    public void EveryAuditedAction_IsAKnownAuditAction()
    {
        var invalid = AuditedRequests()
            .Where(item => !Enum.TryParse<AuditAction>(item.Audit.Action, ignoreCase: true, out _))
            .Select(item => $"{item.Request.Name} → \"{item.Audit.Action}\"")
            .ToList();

        invalid.Should().BeEmpty("a ação precisa ser um membro de AuditAction, senão a auditoria falha em silêncio");
    }

    [Fact]
    public void ActionFromProperty_PointsToARealStringProperty()
    {
        var invalid = AuditedRequests()
            .Where(item => item.Audit.ActionFromProperty is { } name
                && item.Request.GetProperty(name)?.PropertyType != typeof(string))
            .Select(item => item.Request.Name)
            .ToList();

        invalid.Should().BeEmpty();
    }

    [Fact]
    public void EveryAuditedRequest_CanResolveTheEntityId()
    {
        // Ou aponta uma propriedade Guid do request (EntityIdProperty), ou o sucesso devolve um tipo com "Id" Guid.
        var unresolved = AuditedRequests()
            .Where(item =>
            {
                if (item.Audit.EntityIdProperty is { } name)
                {
                    return item.Request.GetProperty(name)?.PropertyType != typeof(Guid);
                }

                var responseType = item.Request.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                    ?.GetGenericArguments()[0];
                var value = responseType is { IsGenericType: true } ? responseType.GetGenericArguments()[0] : null;

                return value?.GetProperty("Id")?.PropertyType != typeof(Guid);
            })
            .Select(item => item.Request.Name)
            .ToList();

        unresolved.Should().BeEmpty(
            "sem EntityIdProperty nem Id na resposta, o AuditBehavior não acha o id e a ação fica sem trilha");
    }
}
