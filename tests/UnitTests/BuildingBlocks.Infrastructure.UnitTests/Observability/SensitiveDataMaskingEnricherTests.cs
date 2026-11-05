using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;
using Serilog;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

/// <summary>
/// Testa o enricher pelo caminho real: um Logger do Serilog com o enricher e um sink que captura
/// o que seria escrito. O que se afirma é "o texto que sai do sink não contém PII", que é o que
/// importa para a LGPD — não a estrutura interna.
/// </summary>
public sealed class SensitiveDataMaskingEnricherTests
{
    private sealed record Contato(string Email, string Telefone);

    private sealed record Cliente(string Nome, string Cpf, Contato Contato, string[] Observacoes);

    private sealed record Credenciais(string Usuario, string Password, string RefreshToken);

    [Fact]
    public void ScalarProperty_WithPii_IsMasked()
    {
        var rendered = Capture(logger => logger.Information("Contato {Valor}", "ana@escritorio.com / (11) 98765-4321"));

        rendered.Should().NotContain("ana@").And.NotContain("98765");
        rendered.Should().Contain("a***@escritorio.com").And.Contain("(11) 9****-4321");
    }

    [Fact]
    public void DestructuredObject_HasNestedPiiMasked()
    {
        var cliente = new Cliente(
            "Ana Souza", "123.456.789-01", new Contato("ana@escritorio.com", "(11) 98765-4321"),
            ["ligar no 11987654321"]);

        var rendered = Capture(logger => logger.Information("Cadastrado {@Cliente}", cliente));

        rendered.Should().Contain("Ana Souza");
        rendered.Should().NotContain("123.456.789-01").And.NotContain("ana@").And.NotContain("98765").And.NotContain("11987654321");
    }

    [Fact]
    public void PropertiesWithSensitiveNames_AreRedactedEvenWithoutARecognizablePattern()
    {
        var credenciais = new Credenciais("ana", "Correct-Horse-Battery", "rt_opaque_value_123");

        var rendered = Capture(logger => logger.Information("Login {@Credenciais} {Password}", credenciais, "outra-senha-qualquer"));

        rendered.Should().NotContain("Correct-Horse-Battery").And.NotContain("rt_opaque_value_123").And.NotContain("outra-senha-qualquer");
        rendered.Should().Contain("ana");
    }

    [Fact]
    public void DictionaryEntries_UseTheKeyNameToDecideRedaction()
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "Basic dXNlcjpwYXNz", ["Accept"] = "application/json" };

        var rendered = Capture(logger => logger.Information("Headers {@Headers}", headers));

        rendered.Should().NotContain("dXNlcjpwYXNz").And.Contain("application/json");
    }

    [Fact]
    public void EventWithoutSensitiveData_KeepsTheSameInstances()
    {
        var value = new Serilog.Events.ScalarValue("tudo certo");

        SensitiveDataMaskingEnricher.Mask("Status", value).Should().BeSameAs(value);
    }

    [Fact]
    public void NonStringScalars_AreNotTouched()
    {
        var rendered = Capture(logger => logger.Information("Respondeu {StatusCode} em {Elapsed}ms", 200, 42));

        rendered.Should().Contain("200").And.Contain("42");
    }

    private static string Capture(Action<ILogger> log)
    {
        var events = new List<LogEvent>();
        using var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataMaskingEnricher>()
            .WriteTo.Sink(new CollectingSink(events))
            .CreateLogger();

        log(logger);

        using var writer = new StringWriter();
        foreach (var logEvent in events)
        {
            logEvent.RenderMessage(writer);
            writer.Write(' ');
            foreach (var property in logEvent.Properties)
            {
                writer.Write($"{property.Key}={property.Value} ");
            }
        }

        return writer.ToString();
    }

    private sealed class CollectingSink(List<LogEvent> events) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }
}
