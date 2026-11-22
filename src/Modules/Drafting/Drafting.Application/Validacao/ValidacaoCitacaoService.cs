using Advocacia.Modules.Drafting.Application.Normas;

namespace Advocacia.Modules.Drafting.Application.Validacao;

public interface IValidacaoCitacaoService
{
    Task<ValidacaoCitacoes> ValidarAsync(
        string textoGerado, ContextoValidacao contexto, CancellationToken cancellationToken = default);
}

/// <summary>A conferência determinística mais o que só o banco responde: a URN citada existe no acervo?</summary>
public sealed class ValidacaoCitacaoService(INormaSearchIndex indice) : IValidacaoCitacaoService
{
    private const int MaxUrns = 20;

    public async Task<ValidacaoCitacoes> ValidarAsync(
        string textoGerado, ContextoValidacao contexto, CancellationToken cancellationToken = default)
    {
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var urn in ValidadorCitacoes.UrnsParaConferir(textoGerado, contexto.Chunks).Take(MaxUrns))
        {
            if (await indice.UrnExisteAsync(urn, cancellationToken))
            {
                existentes.Add(urn);
            }
        }

        return ValidadorCitacoes.Validar(textoGerado, contexto with { UrnsConhecidas = existentes });
    }
}
