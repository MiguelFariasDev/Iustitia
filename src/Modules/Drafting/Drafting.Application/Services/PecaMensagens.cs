using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Textos mostrados ao usuário. Nunca carregam detalhe técnico (modelo, GPU, endereço, exceção).</summary>
public static class PecaMensagens
{
    public const string FalhaGenerica = "Não foi possível gerar a peça. Tente novamente.";

    public static string Para(LegalDocumentStatus status) => status switch
    {
        LegalDocumentStatus.Pendente => "Sua solicitação está na fila de processamento.",
        LegalDocumentStatus.NaFila => "Sua solicitação foi reservada para processamento.",
        LegalDocumentStatus.PreparandoGpu => "Preparando o ambiente de IA.",
        LegalDocumentStatus.BuscandoContexto => "A IA está analisando os documentos relevantes.",
        LegalDocumentStatus.Gerando => "Gerando a minuta.",
        LegalDocumentStatus.Salvando => "Salvando a minuta.",
        LegalDocumentStatus.Concluido => "Minuta pronta para revisão profissional.",
        LegalDocumentStatus.Falhou => FalhaGenerica,
        LegalDocumentStatus.Cancelado => "Solicitação cancelada.",
        _ => string.Empty,
    };
}

/// <summary>Falha da geração que vale repetir (ex.: o modelo terminou sem devolver texto).</summary>
public sealed class TransientGenerationException(string message) : Exception(message);
