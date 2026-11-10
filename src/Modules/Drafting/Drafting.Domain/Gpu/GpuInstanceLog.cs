using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Gpu;

/// <summary>
/// Registro de cada operação de controle da instância de GPU (iniciar, parar, falha, circuito
/// aberto…). É infraestrutura COMPARTILHADA entre os escritórios, então não tem tenant nem RLS:
/// nunca contém dado de caso, só ids de instância, estados e motivos.
/// </summary>
public sealed class GpuInstanceLog : Entity<Guid>
{
    public string? VastInstanceId { get; private set; }

    public string Acao { get; private set; } = string.Empty;

    public string? StatusAnterior { get; private set; }

    public string? StatusNovo { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; private set; }

    public bool Sucesso { get; private set; }

    public string? Erro { get; private set; }

    private GpuInstanceLog()
    {
        // EF Core.
    }

    public GpuInstanceLog(
        string? vastInstanceId, string acao, string? statusAnterior, string? statusNovo, string motivo,
        DateTimeOffset criadoEm, bool sucesso, string? erro)
        : base(Guid.NewGuid())
    {
        VastInstanceId = vastInstanceId;
        Acao = acao;
        StatusAnterior = statusAnterior;
        StatusNovo = statusNovo;
        Motivo = motivo.Length > 300 ? motivo[..300] : motivo;
        CriadoEm = criadoEm;
        Sucesso = sucesso;
        Erro = erro is { Length: > 500 } ? erro[..500] : erro;
    }
}
