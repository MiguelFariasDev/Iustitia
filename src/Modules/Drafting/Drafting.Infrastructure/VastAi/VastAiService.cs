using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Gpu;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.VastAi;

/// <summary>
/// Cliente da API da Vast.ai para UMA instância. Só usa o que a Vast.ai documenta (conferido no
/// código-fonte do CLI oficial, vast-ai/vast-cli):
/// <list type="bullet">
/// <item><c>GET  {ApiBaseUrl}/instances/{id}/?owner=me</c> → <c>{"instances": {…}}</c> com
///   <c>actual_status</c>, <c>intended_status</c>, <c>public_ipaddr</c> e <c>ports</c>
///   (ex.: <c>"11434/tcp": [{"HostPort": "41234"}]</c>);</item>
/// <item><c>PUT  {ApiBaseUrl}/instances/{id}/</c> com <c>{"state":"running"}</c> (iniciar) ou
///   <c>{"state":"stopped"}</c> (parar) → <c>{"success": true|false, "msg": …}</c>;</item>
/// <item>autenticação: <c>Authorization: Bearer &lt;chave&gt;</c>.</item>
/// </list>
/// Parar PRESERVA a instância (disco e modelos); este serviço nunca a destrói. Iniciar uma instância
/// ativa e parar uma parada não chamam a API. Falhas de rede/5xx/429 são repetidas com espera
/// crescente; toda operação é registrada em <c>gpu_instance_logs</c>.
/// </summary>
public sealed class VastAiService(
    HttpClient httpClient,
    IOptions<VastAiOptions> options,
    IGpuInstanceLogRepository logs,
    IGpuStateService state,
    IDateTimeProvider clock,
    ILogger<VastAiService> logger) : IVastAiService
{
    public async Task<Result<VastInstanceInfo>> ObterInstanciaAsync(CancellationToken cancellationToken = default)
    {
        var configured = EnsureConfigured();
        if (configured is not null)
        {
            return Result.Failure<VastInstanceInfo>(configured);
        }

        var response = await SendWithRetryAsync(
            () => Request(HttpMethod.Get, $"instances/{options.Value.InstanceId}/?owner=me"), cancellationToken);
        if (response is null)
        {
            await LogAsync("Consultar", null, null, "Consulta do estado", false, "falha de rede/HTTP", cancellationToken);
            state.RegistrarFalha("Vast.ai indisponível", clock.UtcNow);

            return Result.Failure<VastInstanceInfo>(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        }

        using (response)
        {
            using var document = await ReadJsonAsync(response, cancellationToken);
            var info = document is null ? null : Parse(document.RootElement);
            if (info is null)
            {
                return Result.Failure<VastInstanceInfo>(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
            }

            state.RegistrarSucesso();

            return Result.Success(info);
        }
    }

    public async Task<Result> IniciarAsync(string motivo, CancellationToken cancellationToken = default)
    {
        var atual = await ObterInstanciaAsync(cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure(atual.Error);
        }

        // Idempotência: não inicia o que já está ativo.
        if (atual.Value.IsRunning)
        {
            logger.LogInformation("Vast.ai: instância já ativa; início ignorado.");

            return Result.Success();
        }

        return await ChangeStateAsync("running", "Iniciar", atual.Value.ActualStatus, motivo, cancellationToken);
    }

    public async Task<Result> PararAsync(string motivo, CancellationToken cancellationToken = default)
    {
        var atual = await ObterInstanciaAsync(cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure(atual.Error);
        }

        if (atual.Value.IsStopped)
        {
            logger.LogInformation("Vast.ai: instância já parada; parada ignorada.");

            return Result.Success();
        }

        return await ChangeStateAsync("stopped", "Parar", atual.Value.ActualStatus, motivo, cancellationToken);
    }

    private async Task<Result> ChangeStateAsync(
        string novoEstado, string acao, string? statusAnterior, string motivo, CancellationToken ct)
    {
        var instanceId = options.Value.InstanceId;
        var response = await SendWithRetryAsync(
            () =>
            {
                var request = Request(HttpMethod.Put, $"instances/{instanceId}/");
                request.Content = JsonContent.Create(new { state = novoEstado });

                return request;
            },
            ct);

        if (response is null)
        {
            await LogAsync(acao, statusAnterior, novoEstado, motivo, false, "falha de rede/HTTP", ct);
            state.RegistrarFalha("Vast.ai indisponível", clock.UtcNow);

            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        }

        using (response)
        {
            using var document = await ReadJsonAsync(response, ct);
            var success = document is not null
                          && document.RootElement.TryGetProperty("success", out var flag)
                          && flag.ValueKind == JsonValueKind.True;

            string? erro = null;
            if (!success && document is not null && document.RootElement.TryGetProperty("msg", out var msg)
                && msg.ValueKind == JsonValueKind.String)
            {
                erro = msg.GetString();
            }

            await LogAsync(acao, statusAnterior, novoEstado, motivo, success, erro, ct);

            if (!success)
            {
                state.RegistrarFalha(erro ?? "Vast.ai recusou a operação", clock.UtcNow);

                return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
            }

            state.RegistrarSucesso();

            return Result.Success();
        }
    }

    /// <summary>Repete falha de rede, 5xx e 429; 4xx (chave, instância inexistente) não adianta repetir.</summary>
    private async Task<HttpResponseMessage?> SendWithRetryAsync(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        var max = Math.Max(1, options.Value.MaxRetries);
        var baseDelay = TimeSpan.FromMilliseconds(Math.Max(0, options.Value.RetryDelayMilliseconds));

        for (var attempt = 1; attempt <= max; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.SendAsync(build(), ct);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var transient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
                logger.LogWarning("Vast.ai: HTTP {Status} (tentativa {Tentativa}/{Max}).", (int)response.StatusCode, attempt, max);
                response.Dispose();
                response = null;

                if (!transient)
                {
                    return null;
                }
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning("Vast.ai: falha de rede ({Tipo}) (tentativa {Tentativa}/{Max}).", exception.GetType().Name, attempt, max);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Vast.ai: timeout (tentativa {Tentativa}/{Max}).", attempt, max);
            }
            finally
            {
                response?.Dispose();
            }

            if (attempt < max)
            {
                await Task.Delay(baseDelay * Math.Pow(2, attempt - 1), ct);
            }
        }

        return null;
    }

    private HttpRequestMessage Request(HttpMethod method, string relative)
    {
        var baseUrl = options.Value.ApiBaseUrl.TrimEnd('/') + "/";
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        return request;
    }

    private Error? EnsureConfigured()
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.InstanceId))
        {
            logger.LogError("Vast.ai: VastAi:ApiKey e VastAi:InstanceId precisam estar configurados (variáveis de ambiente/Key Vault).");

            return ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE);
        }

        if (!Uri.TryCreate(o.ApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            // A chave da Vast.ai controla dinheiro: só trafega por TLS.
            logger.LogError("Vast.ai: VastAi:ApiBaseUrl precisa ser https://.");

            return ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE);
        }

        return null;
    }

    private static async Task<JsonDocument?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Lê <c>{"instances": {...}}</c>. Instância inexistente/destruída vem como <c>null</c> e vira "indisponível".</summary>
    internal static VastInstanceInfo? Parse(JsonElement root)
    {
        if (!root.TryGetProperty("instances", out var instance) || instance.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? Text(string name) =>
            instance.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        var ports = new Dictionary<string, int>(StringComparer.Ordinal);
        if (instance.TryGetProperty("ports", out var mapping) && mapping.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in mapping.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
                {
                    continue;
                }

                var first = entry.Value[0];
                if (first.ValueKind == JsonValueKind.Object
                    && first.TryGetProperty("HostPort", out var hostPort)
                    && int.TryParse(hostPort.ValueKind == JsonValueKind.String ? hostPort.GetString() : hostPort.ToString(), out var port))
                {
                    ports[entry.Name] = port;
                }
            }
        }

        var id = instance.TryGetProperty("id", out var idValue) ? idValue.ToString() : string.Empty;

        return new VastInstanceInfo(id, Text("actual_status"), Text("intended_status"), Text("public_ipaddr"), ports);
    }

    private async Task LogAsync(
        string acao, string? anterior, string? novo, string motivo, bool sucesso, string? erro, CancellationToken ct)
    {
        try
        {
            logs.Add(new GpuInstanceLog(options.Value.InstanceId, acao, anterior, novo, motivo, clock.UtcNow, sucesso, erro));
            await logs.SaveAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // O registro é auditoria operacional; não pode impedir a operação em si.
            logger.LogError(exception, "Vast.ai: não foi possível gravar o log da operação {Acao}.", acao);
        }
    }
}
