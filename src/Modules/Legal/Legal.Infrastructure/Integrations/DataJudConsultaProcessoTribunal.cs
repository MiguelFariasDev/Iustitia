using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations;

/// <summary>
/// Adaptador entre a porta do módulo Legal (<see cref="IConsultaProcessoTribunal"/>) e a
/// API Pública do DataJud. Tudo que é vocabulário do DataJud — códigos de tabela unificada,
/// níveis de sigilo, envelope de busca — para aqui; o que segue para o handler é o
/// vocabulário do escritório.
///
/// USO RESTRITO A PORTFÓLIO (ver ADR-064). Quando a fonte mudar, só esta classe muda.
/// </summary>
public sealed class DataJudConsultaProcessoTribunal(IDataJudClient client) : IConsultaProcessoTribunal
{
    /// <summary>Quantos movimentos seguem para a tela — a timeline mostra os mais recentes.</summary>
    private const int MaxMovimentos = 10;

    public async Task<Result<ProcessoTribunal?>> ConsultarAsync(
        CNJNumber cnjNumber, CancellationToken cancellationToken = default)
    {
        // O tribunal sai do próprio número (Resolução 65/2008, campo J.TR). Nulo aqui é
        // número estruturalmente válido apontando para tribunal inexistente — o cliente
        // devolve CNJ_TRIBUNAL_NOT_SUPPORTED com a sigla vazia, e a mensagem fica pior.
        var tribunal = cnjNumber.Tribunal;
        if (tribunal is null)
        {
            return Result.Failure<ProcessoTribunal?>(
                ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, $"{cnjNumber.Segment}.{cnjNumber.Court}"));
        }

        var resultado = await client.ConsultarProcessoAsync(tribunal, cnjNumber.Digits, cancellationToken);

        return resultado.IsFailure
            ? Result.Failure<ProcessoTribunal?>(resultado.Error)
            : Result.Success(Traduzir(resultado.Value, cnjNumber));
    }

    private static ProcessoTribunal? Traduzir(DataJudProcesso? processo, CNJNumber cnjNumber)
    {
        if (processo is null)
        {
            return null;
        }

        return new ProcessoTribunal(
            // Devolve o número MASCARADO: é a forma canônica do sistema e o que a tela exibe.
            // O DataJud responde com 20 dígitos crus.
            cnjNumber.Value,
            processo.Tribunal,
            processo.Grau,
            processo.DataAjuizamento is { } ajuizamento ? DateOnly.FromDateTime(ajuizamento.Date) : null,
            processo.EmSegredoDeJustica,
            processo.Classe?.Nome,
            processo.Sistema?.Nome,
            processo.OrgaoJulgador?.Nome,
            processo.Assuntos.Select(assunto => assunto.Nome).ToArray(),
            processo.Movimentos
                .Take(MaxMovimentos)
                .Select(movimento => new MovimentoProcesso(movimento.Nome, movimento.DataHora))
                .ToArray(),
            processo.OrgaoJulgador?.CodigoMunicipioIBGE);
    }
}
