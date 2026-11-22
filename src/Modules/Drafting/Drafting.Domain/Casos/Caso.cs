using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Drafting.Domain.Casos;

public readonly record struct CasoId(Guid Value)
{
    public static CasoId New() => new(Guid.NewGuid());

    public static CasoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public enum CasoStatus
{
    Aberto = 0,
    Encerrado = 1,
}

/// <summary>O que um membro convidado pode fazer no caso.</summary>
public enum CasoPermissao
{
    /// <summary>Vê documentos e minutas e pode pedir minutas.</summary>
    Leitura = 0,

    /// <summary>Além de ler, anexa, revisa e remove documentos.</summary>
    Escrita = 1,
}

/// <summary>
/// A unidade de ISOLAMENTO do módulo: todo anexo, todo trecho indexado e toda minuta pertencem a um
/// caso, e o RAG dos anexos nunca cruza dois casos — nem do mesmo cliente, nem do mesmo escritório.
/// Quem enxerga: a pessoa responsável, os membros explicitamente autorizados e a gestão do escritório
/// (Owner/Partner, por papel). O caso pode existir antes de haver processo (petição inicial).
/// </summary>
public sealed class Caso : AggregateRoot<CasoId>, IAuditable, IHasTenant
{
    public const int TituloMaxLength = 200;
    public const int ClienteNomeMaxLength = 200;
    public const int NumeroProcessoMaxLength = 40;

    private readonly List<CasoMembro> _membros = [];

    public Guid TenantId { get; private set; }

    /// <summary>Cliente do escritório (id do módulo Legal; não há FK entre módulos — ADR-001).</summary>
    public Guid ClienteId { get; private set; }

    /// <summary>Nome exibido no cabeçalho do Markdown dos anexos. Cópia, não fonte da verdade.</summary>
    public string? ClienteNome { get; private set; }

    public Guid UsuarioIdResponsavel { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Area { get; private set; } = string.Empty;

    public Guid? ProcessoId { get; private set; }

    public string? NumeroProcesso { get; private set; }

    /// <summary>UF do caso: prioriza jurisprudência e legislação locais no reranking.</summary>
    public string? Uf { get; private set; }

    public CasoStatus Status { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public IReadOnlyCollection<CasoMembro> Membros => _membros;

    private Caso()
    {
        // EF Core.
    }

    private Caso(CasoId id)
        : base(id)
    {
    }

    public static Result<Caso> Criar(
        Guid tenantId, Guid usuarioId, Guid clienteId, string titulo, string area, string? clienteNome,
        Guid? processoId, string? numeroProcesso, string? uf, DateTimeOffset agora)
    {
        if (clienteId == Guid.Empty)
        {
            return Result.Failure<Caso>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "clienteId"));
        }

        if (string.IsNullOrWhiteSpace(titulo))
        {
            return Result.Failure<Caso>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "titulo"));
        }

        return Result.Success(new Caso(CasoId.New())
        {
            TenantId = tenantId,
            UsuarioIdResponsavel = usuarioId,
            ClienteId = clienteId,
            ClienteNome = string.IsNullOrWhiteSpace(clienteNome) ? null : clienteNome,
            Titulo = titulo,
            Area = area,
            ProcessoId = processoId,
            NumeroProcesso = string.IsNullOrWhiteSpace(numeroProcesso) ? null : numeroProcesso,
            Uf = string.IsNullOrWhiteSpace(uf) ? null : uf.ToUpperInvariant(),
            Status = CasoStatus.Aberto,
            CriadoEm = agora,
        });
    }

    public void Atualizar(string titulo, string area, string? numeroProcesso, string? uf)
    {
        Titulo = titulo;
        Area = area;
        NumeroProcesso = string.IsNullOrWhiteSpace(numeroProcesso) ? null : numeroProcesso;
        Uf = string.IsNullOrWhiteSpace(uf) ? null : uf.ToUpperInvariant();
    }

    public void Encerrar(DateTimeOffset agora)
    {
        Status = CasoStatus.Encerrado;
        EncerradoEm ??= agora;
    }

    public void Reabrir()
    {
        Status = CasoStatus.Aberto;
        EncerradoEm = null;
    }

    public CasoMembro? Membro(Guid usuarioId) => _membros.FirstOrDefault(m => m.UsuarioId == usuarioId);

    public bool PodeLer(Guid? usuarioId) =>
        usuarioId is { } id && (id == UsuarioIdResponsavel || Membro(id) is not null);

    public bool PodeEscrever(Guid? usuarioId) =>
        usuarioId is { } id && (id == UsuarioIdResponsavel || Membro(id)?.Permissao == CasoPermissao.Escrita);

    /// <summary>Concede (ou muda) a permissão de um usuário. Devolve o membro só quando é novo (para o EF inserir).</summary>
    public CasoMembro? Conceder(Guid usuarioId, CasoPermissao permissao, Guid concedidoPor, DateTimeOffset agora)
    {
        if (Membro(usuarioId) is { } existente)
        {
            existente.Alterar(permissao);

            return null;
        }

        var membro = new CasoMembro(TenantId, Id, usuarioId, permissao, concedidoPor, agora);
        _membros.Add(membro);

        return membro;
    }

    public bool Revogar(Guid usuarioId) => _membros.RemoveAll(m => m.UsuarioId == usuarioId) > 0;
}

/// <summary>Autorização granular: um usuário com acesso a UM caso.</summary>
public sealed class CasoMembro : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public CasoId CasoId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public CasoPermissao Permissao { get; private set; }

    public Guid ConcedidoPor { get; private set; }

    public DateTimeOffset ConcedidoEm { get; private set; }

    private CasoMembro()
    {
        // EF Core.
    }

    internal CasoMembro(
        Guid tenantId, CasoId casoId, Guid usuarioId, CasoPermissao permissao, Guid concedidoPor, DateTimeOffset agora)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        CasoId = casoId;
        UsuarioId = usuarioId;
        Permissao = permissao;
        ConcedidoPor = concedidoPor;
        ConcedidoEm = agora;
    }

    internal void Alterar(CasoPermissao permissao) => Permissao = permissao;
}
