using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Prazos;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Legal.Api;

public sealed record CalcularPrazoRequest(
    DateOnly DataBase,
    int Dias,
    string? Tipo,
    string? Referencia,
    IReadOnlyList<DateOnly>? FeriadosLocais);

public sealed record CalcularPrazoResponse(
    DateOnly Vencimento,
    DateOnly InicioDaContagem,
    DateOnly? DataDaPublicacao,
    int DiasContados,
    int DiasDeRecessoIgnorados,
    IReadOnlyList<string> Observacoes);

/// <summary>
/// Calculadora de prazo processual (CPC): determinística, sem IA e sem acesso a dados do escritório.
/// É estimativa para o advogado conferir — a resposta sempre diz isso.
/// </summary>
public static class PrazosEndpoints
{
    /// <summary>Teto de feriados locais por chamada: evita usar a calculadora para varrer o calendário.</summary>
    private const int MaxFeriadosLocais = 60;

    public static IEndpointRouteBuilder MapPrazosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/prazos", "Prazos");

        group.MapPost("/calcular", (CalcularPrazoRequest request) =>
            {
                var tipo = ParseTipo(request.Tipo);
                var referencia = ParseReferencia(request.Referencia);
                if (tipo is null || referencia is null || request.Dias is < 1 or > CalculadoraPrazo.MaxDias
                    || request.FeriadosLocais is { Count: > MaxFeriadosLocais }
                    || request.DataBase.Year is < 2000 or > 2100)
                {
                    return Result.Failure<CalcularPrazoResponse>(ErrorFactory.From(ErrorCode.VALIDATION_OUT_OF_RANGE)).ToHttpResult();
                }

                var resultado = CalculadoraPrazo.Calcular(
                    request.DataBase,
                    request.Dias,
                    tipo.Value,
                    referencia.Value,
                    request.FeriadosLocais is null ? null : new HashSet<DateOnly>(request.FeriadosLocais));

                return Result.Success(new CalcularPrazoResponse(
                    resultado.Vencimento, resultado.InicioDaContagem, resultado.DataDaPublicacao, resultado.DiasContados,
                    resultado.DiasDeRecessoIgnorados, resultado.Observacoes)).ToHttpResult();
            })
            .WithName("CalcularPrazo")
            .WithSummary("Calcula o vencimento de um prazo processual (CPC arts. 219, 220 e 224). Estimativa: confira feriados locais.")
            .WithDescription("tipo: uteis (padrão) | corridos. referencia: ciencia (padrão) | disponibilizacao. feriadosLocais: datas AAAA-MM-DD sem expediente na comarca.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }

    private static TipoContagem? ParseTipo(string? valor) => valor?.ToLowerInvariant() switch
    {
        null or "" or "uteis" => TipoContagem.DiasUteis,
        "corridos" => TipoContagem.DiasCorridos,
        _ => null,
    };

    private static ReferenciaDoPrazo? ParseReferencia(string? valor) => valor?.ToLowerInvariant() switch
    {
        null or "" or "ciencia" => ReferenciaDoPrazo.Ciencia,
        "disponibilizacao" => ReferenciaDoPrazo.Disponibilizacao,
        _ => null,
    };
}
