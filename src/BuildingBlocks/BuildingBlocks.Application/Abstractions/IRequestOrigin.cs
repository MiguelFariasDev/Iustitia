namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// De onde veio a requisição em curso (IP e User-Agent), para a trilha de auditoria — que
/// precisa do IP COMPLETO (é evidência: Provimento CFOAB/LGPD, base legal de segurança), ao
/// contrário dos logs de aplicação, onde só o /24 é registrado. Fora de uma requisição HTTP
/// (jobs, consumers) ambos são nulos.
/// </summary>
public interface IRequestOrigin
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}

/// <summary>Padrão seguro para contextos sem HTTP.</summary>
public sealed class NoRequestOrigin : IRequestOrigin
{
    public string? IpAddress => null;

    public string? UserAgent => null;
}
