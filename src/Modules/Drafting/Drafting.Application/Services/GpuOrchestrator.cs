using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// A política de economia: a GPU só fica ligada enquanto há trabalho.
///
/// <list type="bullet">
/// <item>Liga sob demanda e ESPERA o Ollama responder (e ter os dois modelos) antes de liberar o trabalho.</item>
/// <item>Desliga depois da carência (<c>VastAi:IdleTimeoutMinutes</c>) sem trabalho aberto na fila.</item>
/// <item>Só PARA a instância — nunca a destrói: disco, modelos e configurações ficam para a próxima subida.</item>
/// <item>Idempotente: instância já ativa não é reiniciada; já parada não é parada de novo.</item>
/// <item>Circuito de proteção: falhas seguidas suspendem as tentativas por alguns minutos.</item>
/// </list>
///
/// A carência é medida pela atividade da FILA no banco (não por um relógio em memória): sobrevive a
/// reinício do worker e funciona com mais de uma réplica.
/// </summary>
public sealed class GpuOrchestrator(
    IVastAiService vast,
    IGpuStateService state,
    IGeracaoPecaFila fila,
    ILlmHealth health,
    IOllamaEndpoint endpoint,
    INotificacaoService notificacao,
    IOptions<VastAiOptions> vastOptions,
    IOptions<OllamaModelsOptions> models,
    IDateTimeProvider clock,
    ILogger<GpuOrchestrator> logger) : IGpuOrchestrator
{
    public async Task<Result> GarantirProntaAsync(
        string motivo, Func<CancellationToken, Task>? aoIniciar, CancellationToken cancellationToken = default)
    {
        var options = vastOptions.Value;
        if (state.CircuitoAberto(clock.UtcNow))
        {
            return Failure("Circuito aberto: a Vast.ai falhou várias vezes seguidas; aguardando para tentar de novo.");
        }

        var deadline = clock.UtcNow.AddMinutes(options.StartTimeoutMinutes);
        var polling = TimeSpan.FromSeconds(Math.Max(0, options.PollingIntervalSeconds));

        if (!options.Enabled)
        {
            // Ollama sempre ligado: não há instância a controlar, só esperar ele responder.
            return await WaitForOllamaAsync(deadline, polling, cancellationToken);
        }

        var info = await vast.ObterInstanciaAsync(cancellationToken);
        if (info.IsFailure)
        {
            return Falhou(info.Error.Message);
        }

        if (!info.Value.IsRunning)
        {
            if (aoIniciar is not null)
            {
                await aoIniciar(cancellationToken);
            }

            var started = await vast.IniciarAsync(motivo, cancellationToken);
            if (started.IsFailure)
            {
                return Falhou(started.Error.Message);
            }

            state.RegistrarInicio(clock.UtcNow);

            while (true)
            {
                info = await vast.ObterInstanciaAsync(cancellationToken);
                if (info.IsSuccess && info.Value.IsRunning)
                {
                    break;
                }

                if (clock.UtcNow >= deadline)
                {
                    return Falhou("A instância não ficou ativa dentro do prazo (VastAi:StartTimeoutMinutes).");
                }

                await Task.Delay(polling, cancellationToken);
            }
        }

        var address = ResolveEndpoint(info.Value);
        if (address.IsFailure)
        {
            return Falhou(address.Error.Message);
        }

        return await WaitForOllamaAsync(deadline, polling, cancellationToken);
    }

    public async Task<bool> PararSeOciosaAsync(CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        if (!vastOptions.Value.Enabled || state.CircuitoAberto(agora))
        {
            return false;
        }

        // Trabalho aberto (pendente, em backoff ou reservado) segura a GPU ligada.
        if (await fila.ContarTrabalhoAbertoAsync(cancellationToken) > 0)
        {
            return false;
        }

        var info = await vast.ObterInstanciaAsync(cancellationToken);
        if (info.IsFailure || !info.Value.IsRunning)
        {
            return false;
        }

        var lastQueue = await fila.UltimaAtividadeAsync(cancellationToken);
        var last = Max(lastQueue, state.UltimoInicio);
        var carencia = TimeSpan.FromMinutes(vastOptions.Value.IdleTimeoutMinutes);
        if (last is { } ultima && agora - ultima < carencia)
        {
            return false;
        }

        var parada = await vast.PararAsync($"Ociosa há mais de {vastOptions.Value.IdleTimeoutMinutes} min, sem trabalho na fila.", cancellationToken);
        if (parada.IsFailure)
        {
            Falhou(parada.Error.Message);

            return false;
        }

        logger.LogInformation("GPU parada por ociosidade (instância preservada).");

        return true;
    }

    private async Task<Result> WaitForOllamaAsync(DateTimeOffset deadline, TimeSpan polling, CancellationToken ct)
    {
        while (true)
        {
            bool healthy;
            try
            {
                healthy = await health.VerificarSaudeAsync(ct);
            }
            catch (InvalidOperationException exception)
            {
                // Endereço do Ollama inválido/inseguro: configuração, não espera.
                return Falhou(exception.Message);
            }

            if (healthy)
            {
                var installed = await health.ListarModelosAsync(ct);
                var faltando = models.Value.Required
                    .Where(required => !installed.Any(name => ModelMatches(name, required)))
                    .ToList();

                if (faltando.Count > 0)
                {
                    // Subir de novo não instala modelo: falha clara, sem esperar o prazo.
                    return Falhou($"Modelos ausentes no Ollama: {string.Join(", ", faltando)}. Rode 'ollama pull' na instância.");
                }

                state.RegistrarSucesso();

                return Result.Success();
            }

            if (clock.UtcNow >= deadline)
            {
                return Falhou("O Ollama não respondeu dentro do prazo (VastAi:StartTimeoutMinutes).");
            }

            await Task.Delay(polling, ct);
        }
    }

    private Result ResolveEndpoint(VastInstanceInfo info)
    {
        var options = vastOptions.Value;
        if (!options.ResolverEndpointPelaVast)
        {
            return Result.Success();
        }

        var key = $"{options.PortaOllama}/tcp";
        if (string.IsNullOrWhiteSpace(info.PublicIp) || !info.PublicPorts.TryGetValue(key, out var port))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        }

        try
        {
            // http:// sobre IP público: o Set recusa, a menos que Ollama:AllowInsecureHttp seja uma escolha explícita.
            endpoint.Set(new Uri($"http://{info.PublicIp}:{port}/"));

            return Result.Success();
        }
        catch (InvalidOperationException exception)
        {
            notificacao.NotificarOperacao(exception.Message);

            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        }
    }

    private Result Falhou(string motivo)
    {
        state.RegistrarFalha(motivo, clock.UtcNow);
        notificacao.NotificarOperacao($"GPU: {motivo}");

        return Failure(motivo);
    }

    private Result Failure(string motivo)
    {
        logger.LogWarning("GPU indisponível: {Motivo}", motivo);

        return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
    }

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : a > b ? a : b;

    /// <summary>"qwen3:8b" instalado como "qwen3:8b"; "bge-m3" instalado como "bge-m3:latest".</summary>
    internal static bool ModelMatches(string installed, string required)
    {
        if (string.Equals(installed, required, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !required.Contains(':')
            && string.Equals(installed, required + ":latest", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Modelos que o Ollama precisa ter instalados para a GPU ser considerada pronta.</summary>
public sealed class OllamaModelsOptions
{
    public IReadOnlyList<string> Required { get; set; } = [];
}
