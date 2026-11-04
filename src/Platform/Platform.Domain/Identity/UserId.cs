namespace Advocacia.Platform.Domain.Identity;

/// <summary>
/// Mesmo Id do usuário no Supabase Auth (não gerado localmente) — ver
/// docs/requisitos/09-modelo-de-dados.md, tabela "users".
/// </summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
