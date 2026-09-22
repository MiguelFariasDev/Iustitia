using Advocacia.Platform.Api.Endpoints;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>Agrupa o registro de todos os endpoints de Platform — chamado a partir de Hosts/Api/Program.cs.</summary>
public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapTenantsEndpoints();
        app.MapUsersEndpoints();
        app.MapAuditEndpoints();
        app.MapSettingsEndpoints();
        app.MapFeatureFlagsEndpoints();

        return app;
    }
}
