using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Drafting.Api.UnitTests;

public class CasosEndpointsTests
{
    private static readonly Guid Outro = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static readonly byte[] Contrato = Encoding.UTF8.GetBytes(
        "CLÁUSULA 5 — PAGAMENTO\n\nA contratante pagará R$ 50.000,00 até 10/03/2024, sob pena de multa de 10%.");

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> Id(HttpResponseMessage response) => (await Json(response)).GetProperty("id").GetGuid();

    [Theory]
    [InlineData("POST", "")]
    [InlineData("GET", "")]
    [InlineData("GET", "/00000000-0000-0000-0000-000000000001/documentos")]
    [InlineData("GET", "/00000000-0000-0000-0000-000000000001/documentos/00000000-0000-0000-0000-000000000002/original")]
    [InlineData("DELETE", "/00000000-0000-0000-0000-000000000001/documentos/00000000-0000-0000-0000-000000000002")]
    public async Task SemAutenticacao_401_EPapelSemPermissao_403(string metodo, string rota)
    {
        await using var host = await EndpointHost.StartAsync();
        var corpo = metodo == "POST" ? new { } : null;

        (await host.Client.SendAsync(host.Send(new HttpMethod(metodo), "/api/v1/casos" + rota, corpo, role: null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.SendAsync(host.Send(new HttpMethod(metodo), "/api/v1/casos" + rota, corpo, role: "Intern"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>O fluxo inteiro do produto, pela API: caso → anexo → leitura → indexação → minuta → revisão → exportação.</summary>
    [Fact]
    public async Task FluxoCompleto_DoAnexoAPecaExportada()
    {
        var llm = new ScriptedLlm(
        [
            "DOS FATOS\nO contrato previa R$ 50.000,00 [contrato.txt, p. s/n].\n",
            "DO DIREITO\nHá ato ilícito [Código Civil, art. 186] e a Súmula 999 do STJ ampara o pedido.\n",
            "FONTES UTILIZADAS\n-\nPONTOS PARA REVISÃO HUMANA\n-",
        ]);
        await using var host = await EndpointHost.StartAsync(llm);
        var caso = await host.AbrirCasoAsync();

        // Anexa: 202, original guardado, nada indexado ainda.
        var enviado = await host.AnexarAsync(caso, "contrato.txt", Contrato);
        enviado.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var documento = await Id(enviado);
        host.Arquivos.Objetos.Should().HaveCount(1);
        host.Store.Pontos.Should().BeEmpty();

        // Worker lê e indexa.
        (await host.RunAnexoAsync(documento)).Kind.Should().Be(WorkOutcomeKind.Concluido);
        var anexo = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}/documentos/{documento}")));
        anexo.GetProperty("status").GetString().Should().Be("Indexado");
        anexo.GetProperty("classificacao").GetProperty("valores")[0].GetString().Should().Be("R$ 50.000,00");
        var markdown = await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}/documentos/{documento}/markdown"));
        markdown.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        markdown.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await markdown.Content.ReadAsStringAsync()).Should().Contain("**Cliente:** Empresa X").And.Contain("CLÁUSULA 5");

        // Gera.
        var peca = await Id(await host.Client.SendAsync(host.Send(
            HttpMethod.Post, "/api/v1/pecas/gerar",
            new { casoId = caso, tipoPeca = "peticao_inicial", area = "civel", fatos = "A contratante não pagou.", pedidos = "Condenação.", documentosIds = new[] { documento } })));
        (await host.RunWorkerAsync(peca)).Kind.Should().Be(WorkOutcomeKind.Concluido);
        var detalhe = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/pecas/{peca}")));
        detalhe.GetProperty("fontes").EnumerateArray().Select(f => f.GetProperty("rotulo").GetString())
            .Should().BeEquivalentTo("contrato.txt, p. s/n", "Código Civil, art. 186");
        detalhe.GetProperty("exportacaoBloqueada").GetBoolean().Should().BeTrue();

        // Exportar antes da revisão: bloqueado. Revisar com pendência: bloqueado.
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/pecas/{peca}/exportar"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await host.Client.SendAsync(host.Send(HttpMethod.Post, $"/api/v1/pecas/{peca}/revisao", new { declaroRevisao = true }))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Decide cada item duvidoso e conclui com o texto corrigido.
        var revisao = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/pecas/{peca}/citacoes")));
        var duvidosas = revisao.GetProperty("citacoes").EnumerateArray().Where(c => c.GetProperty("bloqueante").GetBoolean()).ToList();
        duvidosas.Should().ContainSingle().Which.GetProperty("status").GetString().Should().Be("PossivelAlucinacao");
        (await host.Client.SendAsync(host.Send(
            HttpMethod.Put, $"/api/v1/pecas/{peca}/citacoes/{duvidosas[0].GetProperty("id").GetGuid()}",
            new { decisao = "rejeitada", justificativa = "Súmula inexistente." }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Client.SendAsync(host.Send(
            HttpMethod.Post, $"/api/v1/pecas/{peca}/revisao", new { declaroRevisao = true, textoFinal = "DO DIREITO\nHá ato ilícito [Código Civil, art. 186]." })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var arquivo = await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/pecas/{peca}/exportar?formato=docx"));
        arquivo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await arquivo.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal((byte)'P', (byte)'K');
    }

    [Fact]
    public async Task OutroAdvogado_Recebe404EmTudoDoCaso_ENaoConsegueGerarComEle()
    {
        await using var host = await EndpointHost.StartAsync();
        var caso = await host.AbrirCasoAsync();
        var documento = await Id(await host.AnexarAsync(caso, "contrato.txt", Contrato));
        await host.RunAnexoAsync(documento);

        foreach (var rota in new[] { "", "/documentos", $"/documentos/{documento}", $"/documentos/{documento}/original", $"/documentos/{documento}/markdown" })
        {
            (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}{rota}", null, "Lawyer", Outro))).StatusCode
                .Should().Be(HttpStatusCode.NotFound, rota);
        }

        (await host.AnexarAsync(caso, "x.txt", [65], user: Outro)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Client.SendAsync(host.Send(
            HttpMethod.Post, "/api/v1/pecas/gerar", new { casoId = caso, tipoPeca = "parecer", area = "civel", fatos = "x" }, "Lawyer", Outro))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // E o documento de um caso não é alcançável pela rota de outro caso de quem pede.
        var casoDoOutro = await host.AbrirCasoAsync(user: Outro);
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{casoDoOutro}/documentos/{documento}", null, "Lawyer", Outro))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        await using var scope = host.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<DraftingDbContext>().EventosSeguranca.CountAsync(e => e.Tipo == "AcessoNegadoAoCaso"))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AcessoConcedido_Le_ERevogado_Perde()
    {
        await using var host = await EndpointHost.StartAsync();
        var caso = await host.AbrirCasoAsync();

        await host.Client.SendAsync(host.Send(HttpMethod.Put, $"/api/v1/casos/{caso}/membros", new { usuarioId = Outro, permissao = "leitura" }));
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}", null, "Lawyer", Outro))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.AnexarAsync(caso, "x.txt", [65], user: Outro)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await host.Client.SendAsync(host.Send(HttpMethod.Delete, $"/api/v1/casos/{caso}/membros/{Outro}"));
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}", null, "Lawyer", Outro))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("virus.exe", "MZ")]
    [InlineData("falso.pdf", "nao sou pdf")]
    [InlineData("pagina.html", "<html>")]
    public async Task ArquivoNaoPermitido_400(string nome, string conteudo)
    {
        await using var host = await EndpointHost.StartAsync();

        var resposta = await host.AnexarAsync(await host.AbrirCasoAsync(), nome, Encoding.UTF8.GetBytes(conteudo));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(resposta)).GetProperty("errorCode").GetString().Should().Be("DRAFTING_UNSUPPORTED_FILE");
        host.Arquivos.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task ArquivoGrande_400_Duplicado_409_SemArquivo_400()
    {
        await using var host = await EndpointHost.StartAsync(maxMegabytes: 1);
        var caso = await host.AbrirCasoAsync();

        (await host.AnexarAsync(caso, "g.txt", Encoding.ASCII.GetBytes(new string('a', 1_500_000)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.AnexarAsync(caso, "a.txt", Contrato)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await host.AnexarAsync(caso, "b.txt", Contrato)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await host.Client.SendAsync(host.Send(HttpMethod.Post, $"/api/v1/casos/{caso}/documentos", new { }))).StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Remover_ApagaDoIndiceEDoStorage()
    {
        await using var host = await EndpointHost.StartAsync();
        var caso = await host.AbrirCasoAsync();
        var documento = await Id(await host.AnexarAsync(caso, "contrato.txt", Contrato));
        await host.RunAnexoAsync(documento);
        host.Store.Pontos.Should().NotBeEmpty();

        (await host.Client.SendAsync(host.Send(HttpMethod.Delete, $"/api/v1/casos/{caso}/documentos/{documento}"))).IsSuccessStatusCode.Should().BeTrue();

        host.Store.Pontos.Should().BeEmpty();
        host.Arquivos.Objetos.Should().BeEmpty();
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, $"/api/v1/casos/{caso}/documentos/{documento}"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
