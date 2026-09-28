using Asp.Versioning;
using Asp.Versioning.Builder;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Ponto único para criar um grupo de endpoints versionado (ver ADR-039): todo recurso de
/// Platform usa <c>/api/v{version:apiVersion}</c> + <see cref="ApiVersion"/> 1.0 — hoje só
/// existe a v1, mas o roteamento já resolve o segmento via
/// <see cref="UrlSegmentApiVersionReader"/> (ver ServiceCollectionExtensions.AddPlatformApi),
/// então adicionar uma v2 no futuro é só chamar <c>.HasApiVersion(new ApiVersion(2))</c> num
/// novo grupo, sem tocar nos existentes.
/// </summary>
public static class VersionedEndpointExtensions
{
    public static RouteGroupBuilder MapVersionedGroup(this IEndpointRouteBuilder app, string resourcePath, string tag)
    {
        var versionSet = app.NewApiVersionSet(tag)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGroup($"/api/v{{version:apiVersion}}{resourcePath}")
            .WithTags(tag)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(new ApiVersion(1));
    }
}
