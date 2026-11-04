namespace Advocacia.Modules.Legal.Application.Features.CapturePublication;

/// <summary>Resultado da captura.</summary>
/// <param name="PublicationId">Id da publicação — a recém-criada ou a que já existia.</param>
/// <param name="Existing">
/// <c>true</c> quando o ExternalId já estava persistido para o tenant. A captura é
/// idempotente de propósito: uma reexecução do job sobre o mesmo período é sucesso, não
/// erro (ver ADR-056).
/// </param>
public sealed record CapturePublicationResponse(Guid PublicationId, bool Existing);
