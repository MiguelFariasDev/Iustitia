using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Pecas;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class RevisaoPecaTests
{
    private sealed class DocxFalso : IExportadorDocx
    {
        public byte[] Gerar(string titulo, string texto, string rodape) => Encoding.UTF8.GetBytes(texto);
    }

    private readonly Mundo _mundo = new();
    private readonly FakeRepository _repository = new();
    private readonly LegalDocumentRequest _peca;
    private readonly CitacaoPeca _duvidosa;

    public RevisaoPecaTests()
    {
        _peca = LegalDocumentRequest.Create(
            Ids.Tenant, Ids.User, "peticao_inicial", "civel", "Fatos.", "Pedidos.", null, null, _mundo.Clock.UtcNow, casoId: _mundo.Caso.Id.Value).Value;
        _peca.AddCitacao("Código Civil, art. 186", CitacaoStatus.Validada, "ok", null, null, null);
        _duvidosa = _peca.AddCitacao("STJ, Súmula nº 999", CitacaoStatus.PossivelAlucinacao, "não consta", null, null, null);
        _peca.Complete("Texto com [STJ, Súmula nº 999].", 10, 0, null, _mundo.Clock.UtcNow);
        _repository.Add(_peca);
    }

    private RevisaoPecaService Para(FakeCurrentUser? user = null)
    {
        user ??= new FakeCurrentUser();

        return new RevisaoPecaService(
            _repository, _mundo.Acesso(user), new FakeUnitOfWork(),
            new AuditoriaIaService(_repository, new FakeUnitOfWork(), _mundo.Clock, NullLogger<AuditoriaIaService>.Instance), new DocxFalso(), user,
            _mundo.Clock);
    }

    private Guid Id => _peca.Id.Value;

    private Task Decidir(string decisao) => Para().DecidirAsync(Id, _duvidosa.Id, new DecidirCitacaoRequest(decisao, "Conferido no site do STJ."));

    [Fact]
    public async Task Exportar_AntesDaRevisao_EBloqueado()
    {
        (await Para().ExportarAsync(Id, "docx")).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REVIEW_REQUIRED));
    }

    [Fact]
    public async Task Concluir_ComCitacaoPendente_EBloqueado()
    {
        var resultado = await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true));

        resultado.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REVIEW_PENDING));
        _peca.RevisadoEm.Should().BeNull();
    }

    [Fact]
    public async Task Concluir_SemDeclaracaoExpressa_EBloqueado()
    {
        await Decidir("confirmada");

        (await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(false))).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REVIEW_CONFIRMATION_REQUIRED));
    }

    [Fact]
    public async Task CitacaoRejeitada_NaoPodeContinuarNoTextoFinal()
    {
        await Decidir("rejeitada");

        (await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true))).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REJECTED_CITATION_PRESENT));
        (await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true, "Texto sem ela."))).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("md")]
    public async Task RevisaoConcluida_LiberaAExportacaoDoTextoAprovado(string formato)
    {
        await Decidir("corrigida");
        (await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true, "Texto aprovado."))).Value.Concluida.Should().BeTrue();

        var arquivo = (await Para().ExportarAsync(Id, formato)).Value;

        Encoding.UTF8.GetString(arquivo.Conteudo).Should().Contain("Texto aprovado.");
        _peca.TextoGerado.Should().Contain("Súmula nº 999", "o que a IA gerou é preservado");
        _repository.Events.Select(e => e.Tipo).Should().Contain(["CitacaoDecidida", "RevisaoConcluida", "Exportada"]);
    }

    [Fact]
    public async Task MudarDecisaoDepoisDeAprovada_ReabreARevisao()
    {
        await Decidir("confirmada");
        await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true));

        await Decidir("rejeitada");

        (await Para().ExportarAsync(Id, "docx")).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REVIEW_REQUIRED));
    }

    [Fact]
    public async Task QuemSoLe_NaoDecideNemConclui()
    {
        var colega = new FakeCurrentUser(Ids.Tenant, Ids.OtherUser, "Lawyer");
        _mundo.Caso.Conceder(Ids.OtherUser, CasoPermissao.Leitura, Ids.User, _mundo.Clock.UtcNow);

        (await Para(colega).ObterAsync(Id)).IsSuccess.Should().BeTrue();
        (await Para(colega).DecidirAsync(Id, _duvidosa.Id, new DecidirCitacaoRequest("confirmada", "ok, conferi"))).Error.Code
            .Should().Be(nameof(ErrorCode.DRAFTING_CASE_FORBIDDEN));
        (await Para(colega).ConcluirAsync(Id, new ConcluirRevisaoRequest(true))).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_CASE_FORBIDDEN));
    }

    [Fact]
    public async Task QuemNaoVeOCaso_NaoSabeQueAPecaExiste()
    {
        var deFora = new FakeCurrentUser(Ids.Tenant, Ids.OtherUser, "Lawyer");

        (await Para(deFora).ObterAsync(Id)).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
        (await Para(deFora).ExportarAsync(Id, "docx")).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
    }

    [Theory]
    [InlineData("pendente", "conferido ok")]
    [InlineData("talvez", "conferido ok")]
    [InlineData("confirmada", "ok")]
    public async Task DecisaoInvalidaOuSemJustificativa_EBarrada(string decisao, string justificativa) =>
        (await Para().DecidirAsync(Id, _duvidosa.Id, new DecidirCitacaoRequest(decisao, justificativa))).IsFailure.Should().BeTrue();

    [Fact]
    public async Task FormatoDesconhecido_EBarrado()
    {
        await Decidir("confirmada");
        await Para().ConcluirAsync(Id, new ConcluirRevisaoRequest(true));

        (await Para().ExportarAsync(Id, "exe")).IsFailure.Should().BeTrue();
    }
}
