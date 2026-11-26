using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Revisão humana obrigatória (Provimento CFOAB 271/2025). Cada citação duvidosa recebe uma decisão com
/// justificativa; só então a revisão pode ser concluída, com declaração expressa, e a peça exportada.
/// Quem revisa precisa ter permissão de ESCRITA no caso: leitura não aprova peça.
/// </summary>
public sealed class RevisaoPecaService(
    ILegalDocumentRepository repository,
    ICasoAcessoService acesso,
    IDraftingUnitOfWork unitOfWork,
    IAuditoriaIaService audit,
    IExportadorDocx docx,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IRevisaoPecaService
{
    public const int TextoFinalMax = 200_000;
    private const int JustificativaMin = 5;

    public async Task<Result<RevisaoResponse>> ObterAsync(Guid pecaId, CancellationToken cancellationToken = default)
    {
        var peca = await CarregarAsync(pecaId, escrita: false, cancellationToken);

        return peca.IsFailure ? Result.Failure<RevisaoResponse>(peca.Error) : Result.Success(ToResponse(peca.Value));
    }

    public async Task<Result<CitacaoResponse>> DecidirAsync(
        Guid pecaId, Guid citacaoId, DecidirCitacaoRequest request, CancellationToken cancellationToken = default)
    {
        var carregada = await CarregarAsync(pecaId, escrita: true, cancellationToken);
        if (carregada.IsFailure)
        {
            return Result.Failure<CitacaoResponse>(carregada.Error);
        }

        if (!Enum.TryParse<DecisaoCitacao>(request.Decisao, ignoreCase: true, out var decisao)
            || !Enum.IsDefined(decisao) || decisao == DecisaoCitacao.Pendente)
        {
            return Result.Failure<CitacaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "decisao"));
        }

        var justificativa = InputSanitizer.Clean(request.Justificativa);
        if (justificativa.Length < JustificativaMin)
        {
            return Result.Failure<CitacaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "justificativa"));
        }

        if (justificativa.Length > CitacaoPeca.JustificativaMaxLength)
        {
            return Result.Failure<CitacaoResponse>(
                ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "justificativa", CitacaoPeca.JustificativaMaxLength));
        }

        var peca = carregada.Value;
        var citacao = peca.Citacoes.FirstOrDefault(c => c.Id == citacaoId);
        if (citacao is null)
        {
            return Result.Failure<CitacaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_CITATION_NOT_FOUND));
        }

        // Mudar uma decisão depois de aprovada reabre a revisão: o texto aprovado não vale mais.
        peca.ReabrirRevisao();
        citacao.Decidir(decisao, justificativa, currentUser.UserId!.Value, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            peca, "CitacaoDecidida", new { citacao = citacao.Id, status = citacao.Status.ToString(), decisao = decisao.ToString() },
            cancellationToken);

        return Result.Success(GeracaoPecaService.ToCitacao(citacao));
    }

    public async Task<Result<RevisaoResponse>> ConcluirAsync(
        Guid pecaId, ConcluirRevisaoRequest request, CancellationToken cancellationToken = default)
    {
        var carregada = await CarregarAsync(pecaId, escrita: true, cancellationToken);
        if (carregada.IsFailure)
        {
            return Result.Failure<RevisaoResponse>(carregada.Error);
        }

        if (!request.DeclaroRevisao)
        {
            return Result.Failure<RevisaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_REVIEW_CONFIRMATION_REQUIRED));
        }

        var peca = carregada.Value;
        var texto = string.IsNullOrWhiteSpace(request.TextoFinal)
            ? peca.TextoGerado ?? string.Empty
            : InputSanitizer.RemoverInvisiveis(request.TextoFinal).Trim();
        if (texto.Length > TextoFinalMax)
        {
            return Result.Failure<RevisaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "textoFinal", TextoFinalMax));
        }

        var concluida = peca.ConcluirRevisao(texto, currentUser.UserId!.Value, clock.UtcNow);
        if (concluida.IsFailure)
        {
            return Result.Failure<RevisaoResponse>(concluida.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            peca,
            "RevisaoConcluida",
            new
            {
                revisor = currentUser.UserId,
                textoAlterado = !string.Equals(texto, peca.TextoGerado, StringComparison.Ordinal),
                decisoes = peca.Citacoes.GroupBy(c => c.Decisao).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            },
            cancellationToken);

        return Result.Success(ToResponse(peca));
    }

    public async Task<Result<ExportacaoPeca>> ExportarAsync(
        Guid pecaId, string formato, CancellationToken cancellationToken = default)
    {
        var carregada = await CarregarAsync(pecaId, escrita: false, cancellationToken);
        if (carregada.IsFailure)
        {
            return Result.Failure<ExportacaoPeca>(carregada.Error);
        }

        var peca = carregada.Value;
        if (peca.RevisadoEm is not { } revisadoEm || peca.TextoRevisado is not { } texto)
        {
            return Result.Failure<ExportacaoPeca>(ErrorFactory.From(ErrorCode.DRAFTING_REVIEW_REQUIRED));
        }

        var nome = $"{peca.TipoPeca}-v{peca.Versao}-{peca.Id.Value.ToString("N")[..8]}";
        var rodape = $"Minuta elaborada com apoio de IA e revisada por profissional em {revisadoEm:dd/MM/yyyy}.";
        ExportacaoPeca? arquivo = (formato ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "docx" => new(
                docx.Gerar(PecaCatalogo.RotuloTipo(peca.TipoPeca), texto, rodape), nome + ".docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            "md" => new(Encoding.UTF8.GetBytes($"{texto}\n\n---\n\n_{rodape}_\n"), nome + ".md", "text/markdown; charset=utf-8"),
            _ => null,
        };
        if (arquivo is null)
        {
            return Result.Failure<ExportacaoPeca>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "formato"));
        }

        await audit.RecordAsync(peca, "Exportada", new { formato, usuario = currentUser.UserId }, cancellationToken);

        return Result.Success(arquivo);
    }

    private async Task<Result<LegalDocumentRequest>> CarregarAsync(Guid pecaId, bool escrita, CancellationToken ct)
    {
        var peca = await repository.GetWithCitationsAsync(LegalDocumentRequestId.From(pecaId), ct);
        if (peca?.CasoId is not { } casoId)
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
        }

        var autorizado = await acesso.AutorizarAsync(casoId, escrita, ct);
        if (autorizado.IsFailure)
        {
            // Sem acesso ao caso, a peça não existe; com leitura apenas, o erro de permissão é útil e não vaza nada.
            return Result.Failure<LegalDocumentRequest>(
                autorizado.Error.Code == nameof(ErrorCode.DRAFTING_CASE_NOT_FOUND)
                    ? ErrorFactory.From(ErrorCode.DRAFTING_REQUEST_NOT_FOUND)
                    : autorizado.Error);
        }

        return peca.Status == LegalDocumentStatus.Concluido
            ? Result.Success(peca)
            : Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_NOT_READY));
    }

    private static RevisaoResponse ToResponse(LegalDocumentRequest peca) =>
        new(
            peca.Id.Value, peca.RevisadoEm is not null, peca.Citacoes.Count(c => !c.Resolvida), peca.RevisadoEm,
            peca.Citacoes.OrderBy(c => c.Ordem).Select(GeracaoPecaService.ToCitacao).ToList());
}
