using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Registro de uso: grava o evento na trilha da solicitação e emite UM log estruturado.
/// O <c>detalhe</c> deve conter só contagens, tempos e identificadores — quem chama é responsável
/// por nunca passar fatos, pedidos, trechos ou texto gerado.
/// </summary>
public sealed class DraftingAuditService(
    ILegalDocumentRepository repository,
    IDraftingUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<DraftingAuditService> logger) : IDraftingAuditService
{
    public async Task RecordAsync(
        LegalDocumentRequest request, string tipo, object? detalhe = null, CancellationToken cancellationToken = default)
    {
        var json = detalhe is null ? null : JsonSerializer.Serialize(detalhe);
        repository.AddChild(request.AddEvent(tipo, clock.UtcNow, json));

        logger.LogInformation(
            "Peças: {Evento} solicitacao={SolicitacaoId} tenant={TenantId} tipo={TipoPeca} versao={Versao} detalhe={Detalhe}",
            tipo, request.Id.Value, request.TenantId, request.TipoPeca, request.Versao, json);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
