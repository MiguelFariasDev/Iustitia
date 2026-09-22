using Advocacia.BuildingBlocks.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Rede de segurança para exceções realmente inesperadas (bug, infraestrutura fora do ar) e
/// para <see cref="AppException"/> (erros excepcionais conhecidos, mas que não fazem parte
/// do fluxo normal de negócio — ver ADR-033). Erros de negócio normais NUNCA chegam aqui:
/// handlers retornam Result/Result{T} e os endpoints (ver ResultExtensions.ToHttpResult) já
/// convertem falhas de Result em ProblemDetails com o status certo, sem lançar exceção. Em
/// produção, uma exceção genuinamente não tratada nunca expõe mensagem/stack trace — só um
/// Detail genérico; já uma AppException expõe sua mensagem (dev ou prod), porque ela vem do
/// próprio catálogo de erros ou de uma mensagem customizada escrita por nós, nunca de
/// detalhes internos de uma falha de infraestrutura.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException appException)
        {
            logger.LogError(
                appException,
                "AppException {ErrorCode} ({ErrorGroup}) em {Method} {Path}",
                appException.Code, appException.Group, context.Request.Method, context.Request.Path);

            var problemDetails = new ProblemDetails
            {
                Status = appException.HttpStatus,
                Title = appException.Code.ToString(),
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Detail = appException.Message,
                Instance = context.Request.Path,
            };
            problemDetails.Extensions["errorGroup"] = appException.Group.ToString();

            context.Response.StatusCode = appException.HttpStatus;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exceção não tratada em {Method} {Path}", context.Request.Method, context.Request.Path);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = nameof(ErrorCode.INTERNAL_UNEXPECTED),
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Detail = environment.IsDevelopment() ? exception.Message : "Ocorreu um erro inesperado. Tente novamente mais tarde.",
                Instance = context.Request.Path,
            };
            problemDetails.Extensions["errorGroup"] = nameof(ErrorGroup.Internal);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
