using System.Collections.Concurrent;
using System.Reflection;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Application.Behaviors;

/// <summary>
/// Grava na trilha de auditoria os requests marcados com <see cref="AuditedAttribute"/> que
/// terminaram em sucesso. Registrado ANTES do TransactionBehavior (ou seja, por fora dele): quando
/// o <c>next()</c> retorna, a transação do negócio já foi confirmada — não se audita o que foi
/// revertido. Ver ADR-030 (ordem do pipeline) e ADR-069.
///
/// Falha ao gravar a auditoria é logada como ERRO e não desfaz a operação já concluída (mesma
/// política de <c>AuditService</c>). Um alarme sobre esse log é o controle compensatório.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse>(
    IAuditService auditService,
    ILogger<AuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, AuditedAttribute?> Metadata = new();

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var attribute = Metadata.GetOrAdd(typeof(TRequest), type => type.GetCustomAttribute<AuditedAttribute>());
        if (attribute is null)
        {
            return await next();
        }

        var response = await next();

        if (response is not Result { IsSuccess: true })
        {
            return response;
        }

        try
        {
            var entityId = ResolveEntityId(request, response, attribute);
            if (entityId is null)
            {
                logger.LogError(
                    "Auditoria não registrada: não foi possível resolver o id da entidade de {Request}.", typeof(TRequest).Name);
                return response;
            }

            await auditService.RecordAsync(
                ResolveAction(request, attribute),
                attribute.EntityType,
                entityId.Value.ToString(),
                after: ResolveDetails(request, attribute),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao gravar a auditoria de {Request}.", typeof(TRequest).Name);
        }

        return response;
    }

    private static AuditAction ResolveAction(TRequest request, AuditedAttribute attribute)
    {
        if (attribute.ActionFromProperty is { } propertyName
            && typeof(TRequest).GetProperty(propertyName)?.GetValue(request) is string value
            && Enum.TryParse<AuditAction>(value, ignoreCase: true, out var fromProperty)
            && Enum.IsDefined(fromProperty))
        {
            return fromProperty;
        }

        return Enum.Parse<AuditAction>(attribute.Action, ignoreCase: true);
    }

    private static Guid? ResolveEntityId(TRequest request, TResponse response, AuditedAttribute attribute)
    {
        if (attribute.EntityIdProperty is { } requestProperty)
        {
            return typeof(TRequest).GetProperty(requestProperty)?.GetValue(request) as Guid?;
        }

        // Criação: o id vem do valor de sucesso (Result<T>.Value.Id).
        var value = response!.GetType().GetProperty(nameof(Result<object>.Value))?.GetValue(response);
        return value?.GetType().GetProperty("Id")?.GetValue(value) as Guid?;
    }

    private static Dictionary<string, object?>? ResolveDetails(TRequest request, AuditedAttribute attribute)
    {
        if (attribute.DetailProperties.Length == 0)
        {
            return null;
        }

        return attribute.DetailProperties.ToDictionary(
            name => name, name => typeof(TRequest).GetProperty(name)?.GetValue(request));
    }
}
