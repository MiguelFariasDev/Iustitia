using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Drafting.Api.UnitTests;

public class DocumentosEndpointsTests
{
    private const string Base = "/api/v1/documentos";

    private static readonly Guid OutroUsuario = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static HttpRequestMessage Upload(
        string nome, string conteudo, string tipo = "jurisprudencia", string? role = "Lawyer", Guid? user = null,
        string? visibilidade = null, bool semArquivo = false)
    {
        var form = new MultipartFormDataContent();
        if (!semArquivo)
        {
            var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(conteudo));
            arquivo.Headers.ContentType = new("text/plain");
            form.Add(arquivo, "arquivo", nome);
        }

        form.Add(new StringContent(tipo), "tipoDocumento");
        form.Add(new StringContent("civel"), "area");
        form.Add(new StringContent("tjsp"), "tribunal");
        form.Add(new StringContent("2024-05-01"), "dataPublicacao");
        form.Add(new StringContent("dano moral, consumidor"), "tags");
        if (visibilidade is not null)
        {
            form.Add(new StringContent(visibilidade), "visibilidade");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/upload") { Content = form };
        EndpointHost.Authenticate(request, role, user);

        return request;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task SemAutenticacao_Retorna401()
    {
        await using var host = await EndpointHost.StartAsync();

        (await host.Client.SendAsync(Upload("a.txt", "texto", role: null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.SendAsync(host.Send(HttpMethod.Get, Base, null, role: null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PapelSemPermissao_Retorna403()
    {
        await using var host = await EndpointHost.StartAsync();

        (await host.Client.SendAsync(Upload("a.txt", "texto", role: "Financial"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_Responde202_GravaMetadadosEEnfileiraAIndexacao()
    {
        await using var host = await EndpointHost.StartAsync();

        var response = await host.Client.SendAsync(Upload("acordao.txt", "Art. 186 do Código Civil: ato ilícito."));
        var json = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var id = json.GetProperty("id").GetGuid();
        json.GetProperty("status").GetString().Should().Be("Pendente");
        var doc = await InDb(host, db => db.IndexedDocuments.SingleAsync());
        doc.Id.Value.Should().Be(id);
        doc.NomeArquivo.Should().Be("acordao.txt");
        doc.TipoDocumento.Should().Be("jurisprudencia");
        doc.Tribunal.Should().Be("TJSP");
        doc.DataPublicacao.Should().Be(new DateOnly(2024, 5, 1));
        doc.TagsJson.Should().Be("[\"dano moral\",\"consumidor\"]");
        doc.UsuarioId.Should().Be(Ids.User);
        doc.TenantId.Should().Be(Ids.Tenant);
        host.Fila.Items.Should().ContainSingle().Which.Tipo.Should().Be(QueueItemKind.Indexacao);
        host.Store.Upserted.Should().BeEmpty("o upload não gera embeddings");
    }

    [Fact]
    public async Task IndexacaoDoWorker_GravaNoQdrantComTenantEVisibilidade_ELimpaOTextoDoBanco()
    {
        await using var host = await EndpointHost.StartAsync();
        var id = (await Json(await host.Client.SendAsync(Upload("acordao.txt", "Art. 186 do Código Civil: ato ilícito.")))).GetProperty("id").GetGuid();

        var outcome = await host.RunIndexingAsync(id);

        outcome.Kind.Should().Be(Advocacia.Modules.Drafting.Application.Abstractions.WorkOutcomeKind.Concluido);
        var ponto = host.Store.Upserted.Should().NotBeEmpty().And.Subject.First();
        ponto.TenantId.Should().Be(Ids.Tenant);
        ponto.Meta.DocumentoOrigemId.Should().Be(id);
        ponto.Meta.Visibilidade.Should().Be("escritorio");
        ponto.Meta.Tags.Should().Equal("dano moral", "consumidor");
        var doc = await InDb(host, db => db.IndexedDocuments.SingleAsync());
        doc.Status.Should().Be(IndexedDocumentStatus.Indexado);
        doc.PaginasJson.Should().BeNull();
        var consulta = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, $"{Base}/{id}")));
        consulta.GetProperty("status").GetString().Should().Be("Indexado");
        consulta.GetProperty("trechos").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Upload_DeConteudoRepetido_Retorna409()
    {
        await using var host = await EndpointHost.StartAsync();
        await host.Client.SendAsync(Upload("a.txt", "mesmo conteúdo jurídico"));

        var response = await host.Client.SendAsync(Upload("copia.txt", "mesmo conteúdo jurídico"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("errorCode").GetString().Should().Be("DRAFTING_DOCUMENT_DUPLICATE");
    }

    [Theory]
    [InlineData("programa.exe")]
    [InlineData("foto.jpg")]
    [InlineData("sem-extensao")]
    public async Task Upload_DeTipoNaoSuportado_Retorna400(string nome)
    {
        await using var host = await EndpointHost.StartAsync();

        var response = await host.Client.SendAsync(Upload(nome, "conteúdo"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errorCode").GetString().Should().Be("DRAFTING_UNSUPPORTED_FILE");
        (await InDb(host, db => db.IndexedDocuments.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Upload_SemArquivoOuComTipoDeDocumentoInvalido_Retorna400()
    {
        await using var host = await EndpointHost.StartAsync();

        var semArquivo = await host.Client.SendAsync(Upload("x.txt", "x", semArquivo: true));
        var tipoRuim = await host.Client.SendAsync(Upload("x.txt", "conteúdo", tipo: "ficcao"));

        semArquivo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tipoRuim.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_AcimaDoLimiteDeTamanho_Retorna400()
    {
        await using var host = await EndpointHost.StartAsync(maxMegabytes: 1);

        var response = await host.Client.SendAsync(Upload("grande.txt", new string('a', (1024 * 1024) + 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errorCode").GetString().Should().Be("DRAFTING_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task DocumentoPrivado_SoOUsuarioQueEnviouVe_NemOSocio()
    {
        await using var host = await EndpointHost.StartAsync();
        var privado = (await Json(await host.Client.SendAsync(Upload("p.txt", "segredo da cliente", visibilidade: "privado")))).GetProperty("id").GetGuid();
        await host.Client.SendAsync(Upload("e.txt", "conteúdo do escritório"));

        var doOutro = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, Base, null, "Lawyer", OutroUsuario)));
        var doDono = await Json(await host.Client.SendAsync(host.Send(HttpMethod.Get, Base, null, "Lawyer")));
        var consultaOutro = await host.Client.SendAsync(host.Send(HttpMethod.Get, $"{Base}/{privado}", null, "Lawyer", OutroUsuario));
        var consultaSocio = await host.Client.SendAsync(host.Send(HttpMethod.Get, $"{Base}/{privado}", null, "Partner", OutroUsuario));

        doOutro.GetArrayLength().Should().Be(1);
        doDono.GetArrayLength().Should().Be(2);
        consultaOutro.StatusCode.Should().Be(HttpStatusCode.NotFound);
        consultaSocio.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remover_ApagaDoIndiceEDoBanco_ESoQuemPode()
    {
        await using var host = await EndpointHost.StartAsync();
        var id = (await Json(await host.Client.SendAsync(Upload("e.txt", "conteúdo do escritório")))).GetProperty("id").GetGuid();

        var intruso = await host.Client.SendAsync(host.Send(HttpMethod.Delete, $"{Base}/{id}", null, "Lawyer", OutroUsuario));
        var dono = await host.Client.SendAsync(host.Send(HttpMethod.Delete, $"{Base}/{id}"));

        intruso.StatusCode.Should().Be(HttpStatusCode.NotFound);
        dono.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await InDb(host, db => db.IndexedDocuments.CountAsync())).Should().Be(0);
        host.Store.Deleted.Should().ContainSingle().Which.Documento.Should().Be(id);
    }

    private static async Task<T> InDb<T>(EndpointHost host, Func<DraftingDbContext, Task<T>> action)
    {
        await using var scope = host.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<DraftingDbContext>());
    }
}
