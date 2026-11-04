using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Verifica que Result.Failure vira ProblemDetails (RFC 7807) com o código do catálogo de
/// erros (ADR-032) e as extensions errorType/errorGroup — ver ResultExtensions.ToHttpResult.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class ErrorHandlingTests(PlatformApiFactory factory)
{
    [Fact]
    public async Task NotFoundError_ReturnsProblemDetailsWithCatalogCodeAndGroup()
    {
        using var client = factory.CreateClient();
        var tenantCommand = new CreateTenantCommand(
            "Escritorio Teste", TestCnpjGenerator.Generate(), "Owner Teste", $"owner-{Guid.NewGuid():N}@teste.com", "Senha123!");
        var created = await (await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", tenantCommand))
            .Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        var token = factory.JwtTokenFactory.CreateAccessToken(created!.AdminUserId, tenantCommand.AdminEmail, created.TenantId, "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("USER_NOT_FOUND");
        problem.Extensions["errorType"]!.ToString().Should().Be("NotFound");
        problem.Extensions["errorGroup"]!.ToString().Should().Be("User");
        problem.Extensions["errorCode"]!.ToString().Should().Be("USER_NOT_FOUND");
        problem.Extensions.Should().ContainKey("correlationId");
        problem.Extensions.Should().ContainKey("timestamp");
    }

    [Fact]
    public async Task ValidationError_ReturnsProblemDetailsWithValidationCatalogCode()
    {
        using var client = factory.CreateClient();

        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", new CreateTenantCommand("", TestCnpjGenerator.Generate(), "Owner", "owner@teste.com", "Senha123!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("VALIDATION_REQUIRED_FIELD");
        problem.Extensions["errorType"]!.ToString().Should().Be("Validation");
    }

    [Fact]
    public async Task ConflictError_FromExternalAuthProvider_StillMapsToHttp409()
    {
        using var client = factory.CreateClient();
        var command = new CreateTenantCommand(
            "Escritorio Teste", TestCnpjGenerator.Generate(), "Owner Teste", $"owner-{Guid.NewGuid():N}@teste.com", "Senha123!");
        await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);

        // Mesmo e-mail — o SignUp do Supabase (real ou fake, ver FakeSupabaseAuthService)
        // retorna Conflict com um código ad-hoc ("auth.email_already_registered", fora do
        // ErrorCatalog — ver comentário em ResultExtensions.ToProblem). Confirma que o
        // Conflict ainda vira 409 mesmo sem uma entrada correspondente no catálogo.
        var duplicateResponse = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command with { Name = "Outro Escritorio" });

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions.Should().NotContainKey("errorGroup");
    }
}
