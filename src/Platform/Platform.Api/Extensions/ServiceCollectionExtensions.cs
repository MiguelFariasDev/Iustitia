using Asp.Versioning;
using Microsoft.OpenApi.Models;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Composition root de Platform.Api: versionamento de API (ver ADR-039), Swagger/OpenAPI e
/// CORS — preocupações de apresentação/transporte que não pertencem à Application layer.
/// MediatR, FluentValidation e Mapster são registrados por Platform.Application.
/// AddPlatformApplication (ver ADR-030) — chamado antes deste método em Program.cs.
/// Autenticação JWT e policies de autorização são registradas separadamente por
/// AddPlatformAuth (Platform.Infrastructure).
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "AdvocaciaCors";

    public static IServiceCollection AddPlatformApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

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
                    e as extensions `errorCode`/`errorGroup`/`correlationId`/`timestamp`
                    classificam o erro e o correlacionam aos logs (ver
                    docs/errors/catalog.md e docs/api/conventions.md).
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

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                        .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "Idempotency-Key")
                        .WithExposedHeaders("X-Correlation-Id")
                        .AllowCredentials();
                }
            });
        });

        return services;
    }
}
