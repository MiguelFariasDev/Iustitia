using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Acompanhamento;

/// <summary>
/// Andamentos pelo DataJud, para tribunal sem cobertura no juscraper (ADR-075, 6.4). O DataJud não
/// tem id de movimento: o id é "datajud:" + SHA-256(código + data/hora ISO + nome), estável enquanto
/// o CNJ não reescrever o movimento. A API não filtra por data; o <c>desde</c> é aplicado aqui.
/// USO RESTRITO A PORTFÓLIO (ADR-064).
/// </summary>
public sealed class DataJudAcompanhamento(IDataJudClient client)
{
    public const string PrefixoId = "datajud:";

    /// <summary>Tamanho máximo do id inteiro (prefixo + hash truncado).</summary>
    public const int TamanhoId = 64;

    internal async Task<Result<AndamentosCapturados>> ObterAsync(
        CNJNumber cnjNumber, DateOnly? desde, CancellationToken cancellationToken)
    {
        if (cnjNumber.Tribunal is not { } tribunal)
        {
            return Result.Failure<AndamentosCapturados>(
                ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, $"{cnjNumber.Segment}.{cnjNumber.Court}"));
        }

        var resultado = await client.ConsultarProcessoAsync(tribunal, cnjNumber.Digits, cancellationToken);
        if (resultado.IsFailure)
        {
            return Result.Failure<AndamentosCapturados>(resultado.Error);
        }

        if (resultado.Value is not { } processo)
        {
            return Result.Success(new AndamentosCapturados(false, FonteConsulta.DataJud, true, []));
        }

        var andamentos = processo.Movimentos
            .Where(m => !string.IsNullOrWhiteSpace(m.Nome))
            .Where(m => desde is null || m.DataHora is null || DateOnly.FromDateTime(m.DataHora.Value.ToOffset(TimeSpan.FromHours(-3)).Date) >= desde)
            .OrderByDescending(m => m.DataHora ?? DateTimeOffset.MinValue)
            .Select(m => new AndamentoCapturado(Id(m), m.Nome.Trim(), null, m.DataHora, processo.Grau, false))
            .ToList();

        return Result.Success(new AndamentosCapturados(true, FonteConsulta.DataJud, true, andamentos));
    }

    internal static string Id(DataJudMovimento movimento)
    {
        var dataHora = movimento.DataHora?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{movimento.Codigo}|{dataHora}|{movimento.Nome.Trim()}"));
        var hash = Convert.ToHexStringLower(bytes);

        return (PrefixoId + hash)[..TamanhoId];
    }
}
