using System.Net;
using System.Net.Http.Json;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Ver ADR-041/IdempotencyMiddleware.</summary>
[Collection(PlatformApiCollection.Name)]
public class IdempotencyTests(PlatformApiFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient();

    private static CreateTenantCommand NewTenantCommand(string emailSuffix) => new(
        Name: "Escritorio Idempotencia",
        Cnpj: TestCnpjGenerator.Generate(),
        AdminName: "Owner Teste",
        AdminEmail: $"owner-idem-{emailSuffix}@teste.com",
        AdminPassword: "Senha123!");

    [Fact]
    public async Task PostTenant_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync("/api/v1/tenants", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostTenant_SameKeySameBody_ReplaysFirstResponse()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await SendWithKeyAsync(client, command, idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await SendWithKeyAsync(client, command, idempotencyKey);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.Should().ContainKey("Idempotency-Replayed");
        (await second.Content.ReadAsStringAsync()).Should().Be(firstBody);
    }

    [Fact]
    public async Task PostTenant_SameKeyDifferentBody_ReturnsConflict()
    {
        using var client = CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await SendWithKeyAsync(client, NewTenantCommand(Guid.NewGuid().ToString("N")), idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await SendWithKeyAsync(client, NewTenantCommand(Guid.NewGuid().ToString("N")), idempotencyKey);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<HttpResponseMessage> SendWithKeyAsync(HttpClient client, CreateTenantCommand command, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(command),
        };
        request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }
}
