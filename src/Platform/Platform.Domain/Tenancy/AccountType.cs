namespace Advocacia.Platform.Domain.Tenancy;

/// <summary>
/// Como a conta atua. <see cref="Solo"/>: advogado(a) autônomo(a), um único usuário (o Owner),
/// identificado(a) por CPF. <see cref="Firm"/>: escritório com equipe, identificado por CNPJ.
/// Tenants anteriores a este campo são <see cref="Firm"/> (ver ADR-068).
/// </summary>
public enum AccountType
{
    Firm,
    Solo,
}
