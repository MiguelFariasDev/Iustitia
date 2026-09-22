using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.BuildingBlocks.Domain.Errors;

/// <summary>
/// Metadados fixos de um <see cref="ErrorCode"/> — a única fonte de verdade para mensagem
/// (pt-BR), status HTTP e <see cref="ErrorType"/> de cada erro do catálogo. Nunca construa
/// esses valores manualmente fora de <see cref="ErrorCatalog"/> — use
/// <see cref="ErrorFactory"/>.
/// </summary>
/// <param name="Group">Grupo ao qual o código pertence (ver <see cref="ErrorGroup"/>).</param>
/// <param name="Code">Nome do código em texto (ex.: "TENANT_NOT_FOUND"), usado como <c>Error.Code</c>.</param>
/// <param name="Message">
/// Mensagem padrão em pt-BR, amigável e sem jargão técnico. Pode conter placeholders
/// (<c>{0}</c>, <c>{1}</c>, ...) para os <c>args</c> de <see cref="ErrorFactory.From"/>.
/// </param>
/// <param name="HttpStatus">Status HTTP correspondente — mesma tabela usada por ResultExtensions.ToHttpResult.</param>
/// <param name="Type">Tipo de erro do Result Pattern (ver <see cref="ErrorType"/>).</param>
public sealed record ErrorDefinition(ErrorGroup Group, string Code, string Message, int HttpStatus, ErrorType Type);
