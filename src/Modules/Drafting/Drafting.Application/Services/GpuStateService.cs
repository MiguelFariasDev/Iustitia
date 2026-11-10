using Advocacia.Modules.Drafting.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Estado em memória da GPU: o circuito de proteção ("circuit breaker" simples) e a última subida.
/// Falhas seguidas ao falar com a Vast.ai ou ao subir a instância abrem o circuito por alguns
/// minutos, para o worker não insistir (e não gastar dinheiro) numa API/instância quebrada.
/// Nada aqui precisa sobreviver a um reinício: o que precisa (atividade da fila) vem do banco.
/// </summary>
public sealed class GpuStateService(IOptions<VastAiOptions> options) : IGpuStateService
{
    private readonly object _gate = new();
    private int _falhasSeguidas;
    private DateTimeOffset? _abertoAte;
    private DateTimeOffset? _ultimoInicio;

    public DateTimeOffset? UltimoInicio
    {
        get
        {
            lock (_gate)
            {
                return _ultimoInicio;
            }
        }
    }

    public bool CircuitoAberto(DateTimeOffset agora)
    {
        lock (_gate)
        {
            if (_abertoAte is null)
            {
                return false;
            }

            if (agora >= _abertoAte)
            {
                // Meio-aberto: deixa UMA tentativa passar; se falhar de novo, reabre.
                _abertoAte = null;
                _falhasSeguidas = Math.Max(0, options.Value.CircuitBreakerFalhas - 1);

                return false;
            }

            return true;
        }
    }

    public void RegistrarFalha(string motivo, DateTimeOffset agora)
    {
        lock (_gate)
        {
            _falhasSeguidas++;
            if (_falhasSeguidas >= options.Value.CircuitBreakerFalhas)
            {
                _abertoAte = agora.AddMinutes(options.Value.CircuitBreakerPausaMinutos);
            }
        }
    }

    public void RegistrarSucesso()
    {
        lock (_gate)
        {
            _falhasSeguidas = 0;
            _abertoAte = null;
        }
    }

    public void RegistrarInicio(DateTimeOffset agora)
    {
        lock (_gate)
        {
            _ultimoInicio = agora;
        }
    }

    public GpuSnapshot Snapshot(DateTimeOffset agora)
    {
        var aberto = CircuitoAberto(agora);

        return new GpuSnapshot(aberto ? "Instável" : "Sob demanda", aberto, UltimoInicio);
    }
}
