using System.Collections.Concurrent;
using System.Text;
using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.Platform.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Advocacia.Platform.Infrastructure.UnitTests.Middleware;

/// <summary>
/// Reenvio do MESMO cadastro (mesma Idempotency-Key) enquanto o primeiro ainda roda: tem de esperar
/// e devolver a mesma resposta, não executar de novo (o que dava "e-mail já cadastrado").
/// </summary>
public sealed class IdempotencyMiddlewareTests
{
    [Fact]
    public async Task ReenvioSimultaneo_ComAMesmaChave_ExecutaUmaVez_EDevolveAMesmaResposta()
    {
        var execucoes = 0;
        var liberar = new TaskCompletionSource();
        var middleware = new IdempotencyMiddleware(async context =>
        {
            Interlocked.Increment(ref execucoes);
            await liberar.Task;
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"tenantId\":\"t-1\"}");
        });
        var cache = new MemoriaCache();
        var chave = Guid.NewGuid().ToString();

        var primeira = Requisicao(chave);
        var segunda = Requisicao(chave);
        var t1 = middleware.InvokeAsync(primeira, cache);
        var t2 = middleware.InvokeAsync(segunda, cache);
        await Task.Delay(50);
        liberar.SetResult();
        await Task.WhenAll(t1, t2);

        execucoes.Should().Be(1);
        segunda.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        segunda.Response.Headers["Idempotency-Replayed"].ToString().Should().Be("true");
        Corpo(segunda).Should().Be("{\"tenantId\":\"t-1\"}");
    }

    [Fact]
    public async Task ChavesDiferentes_NaoEsperamUmaPelaOutra()
    {
        var execucoes = 0;
        var middleware = new IdempotencyMiddleware(context =>
        {
            Interlocked.Increment(ref execucoes);
            context.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        });
        var cache = new MemoriaCache();

        await Task.WhenAll(
            middleware.InvokeAsync(Requisicao(Guid.NewGuid().ToString()), cache),
            middleware.InvokeAsync(Requisicao(Guid.NewGuid().ToString()), cache));

        execucoes.Should().Be(2);
    }

    private static DefaultHttpContext Requisicao(string chave)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/tenants";
        context.Request.Headers["Idempotency-Key"] = chave;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"adminEmail\":\"novo@exemplo.com\"}"));
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static string Corpo(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private sealed class MemoriaCache : ICacheService
    {
        private readonly ConcurrentDictionary<string, object?> _itens = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_itens.TryGetValue(key, out var valor) ? (T?)valor : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _itens[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _itens.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(
            string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var valor = await factory(cancellationToken);
            _itens[key] = valor;
            return valor;
        }
    }
}
