using Microsoft.OpenApi.Models;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Composition root de Platform.Api: só Swagger/OpenAPI, uma preocupação de apresentação
/// que não pertence à Application layer. MediatR, FluentValidation e Mapster são
/// registrados por Platform.Application.AddPlatformApplication (ver ADR-030) — chamado
/// antes deste método em Program.cs. Autenticação JWT e policies de autorização são
/// registradas separadamente por AddPlatformAuth (Platform.Infrastructure).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Iustitia API",
                Version = "v1",
                Description = """
                    Toda falha (validação, regra de negócio ou erro interno) é retornada como
                    ProblemDetails (RFC 7807): `title` é o código do catálogo de erros (ex.:
                    "TENANT_NOT_FOUND"), `detail` é a mensagem em pt-BR, `status` é o HTTP status
                    e as extensions `errorType`/`errorGroup` classificam o erro (ver
                    docs/errors/catalog.md para a lista completa de códigos, por grupo).
                    """,
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token emitido pelo Supabase Auth (sem o prefixo \"Bearer \").",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    []
                },
            });
        });

        return services;
    }
}
