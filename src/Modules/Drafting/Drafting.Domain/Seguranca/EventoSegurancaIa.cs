using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Seguranca;

/// <summary>
/// Tentativa suspeita (injeção de prompt no formulário ou dentro de um anexo, acesso negado a um caso).
/// Guarda a ORIGEM e o hash do conteúdo, nunca o conteúdo: o registro não pode virar vazamento.
/// </summary>
public sealed class EventoSegurancaIa : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    /// <summary>Ex.: "InjecaoNaEntrada", "InjecaoEmAnexo", "AcessoNegado".</summary>
    public string Tipo { get; private set; } = string.Empty;

    /// <summary>Campo ou documento onde ocorreu (nome do campo ou id).</summary>
    public string Origem { get; private set; } = string.Empty;

    public string? HashConteudo { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    private EventoSegurancaIa()
    {
        // EF Core.
    }

    public EventoSegurancaIa(
        Guid tenantId, Guid? usuarioId, string tipo, string origem, string? hashConteudo, DateTimeOffset agora)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        UsuarioId = usuarioId;
        Tipo = tipo;
        Origem = origem.Length > 200 ? origem[..200] : origem;
        HashConteudo = hashConteudo;
        OcorridoEm = agora;
    }
}
