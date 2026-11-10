using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Normas;

/// <summary>Uma execução de sincronização do acervo: o que foi visto, processado, ignorado e quebrou.</summary>
public sealed class IngestaoJob : Entity<Guid>
{
    public IngestaoJobTipo Tipo { get; private set; }

    public IngestaoJobStatus Status { get; private set; }

    public int TotalArquivos { get; private set; }

    public int ArquivosProcessados { get; private set; }

    public int ArquivosComErro { get; private set; }

    public int ArquivosIgnorados { get; private set; }

    public int ArquivosRemovidos { get; private set; }

    public string? CommitOrigem { get; private set; }

    public string? Escopo { get; private set; }

    public DateTimeOffset IniciadoEm { get; private set; }

    public DateTimeOffset? ConcluidoEm { get; private set; }

    public string? Erro { get; private set; }

    private IngestaoJob()
    {
        // EF Core.
    }

    public IngestaoJob(IngestaoJobTipo tipo, string? escopo, DateTimeOffset agora)
        : base(Guid.NewGuid())
    {
        Tipo = tipo;
        Escopo = escopo is { Length: > 500 } ? escopo[..500] : escopo;
        Status = IngestaoJobStatus.EmAndamento;
        IniciadoEm = agora;
    }

    /// <summary>Pedido manual (API): fica "Pendente" até o worker pegar e chamar <see cref="Iniciar"/>.</summary>
    public static IngestaoJob Solicitar(IngestaoJobTipo tipo, DateTimeOffset agora) =>
        new(tipo, null, agora) { Status = IngestaoJobStatus.Pendente };

    public void Iniciar(string? escopo, DateTimeOffset agora)
    {
        Status = IngestaoJobStatus.EmAndamento;
        Escopo = escopo is { Length: > 500 } ? escopo[..500] : escopo;
        IniciadoEm = agora;
    }

    public void DefinirTotal(int total, string? commit)
    {
        TotalArquivos = total;
        CommitOrigem = commit;
    }

    public void Processado() => ArquivosProcessados++;

    public void Ignorado() => ArquivosIgnorados++;

    public void ComErro() => ArquivosComErro++;

    public void Removido() => ArquivosRemovidos++;

    public void Concluir(DateTimeOffset agora, bool interrompido = false)
    {
        Status = interrompido ? IngestaoJobStatus.Cancelado
            : ArquivosComErro > 0 ? IngestaoJobStatus.ConcluidoComErros : IngestaoJobStatus.Concluido;
        ConcluidoEm = agora;
    }

    public void Falhar(string erro, DateTimeOffset agora)
    {
        Status = IngestaoJobStatus.Falhou;
        Erro = erro.Length > 500 ? erro[..500] : erro;
        ConcluidoEm = agora;
    }
}
