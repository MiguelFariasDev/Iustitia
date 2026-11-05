using Serilog.Core;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Aplica <see cref="LogMaskingPolicy"/> a TODO valor de propriedade de cada log event, antes de
/// qualquer sink renderizar a mensagem — cobre propriedades escalares, objetos destruturados
/// (<c>{@Cliente}</c>), listas e dicionários, em qualquer profundidade (ver ADR-037).
///
/// Dois mecanismos complementares:
///   1. Por CONTEÚDO: toda string passa pela regex da política (CPF, e-mail, telefone, token...).
///   2. Por NOME: propriedade cujo nome é sensível (password, token, cpf, documento, oab...) tem
///      o valor suprimido por inteiro, mesmo que não case com nenhum padrão — uma senha não tem
///      "formato".
///
/// Este enricher é o equivalente prático da "Destructuring Policy" pedida na Etapa 3.9: uma
/// IDestructuringPolicy só vê objetos destruturados com <c>@</c>; aqui o resultado final é
/// higienizado, seja qual for o caminho que o dado percorreu até virar propriedade.
///
/// LIMITAÇÃO conhecida: o texto do <em>template</em> da mensagem não é alterado. Interpolar
/// dado pessoal no template (<c>$"cliente {cpf}"</c>) vaza; o correto é sempre o template
/// estruturado (<c>"cliente {Cpf}"</c>), que passa por aqui.
/// </summary>
public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            var masked = Mask(name, value);
            if (!ReferenceEquals(masked, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
            }
        }
    }

    /// <summary>Devolve a MESMA instância quando nada mudou (evita realocar a árvore inteira em todo log).</summary>
    public static LogEventPropertyValue Mask(string? propertyName, LogEventPropertyValue value)
    {
        if (propertyName is not null && LogMaskingPolicy.IsSensitivePropertyName(propertyName) && value is not StructureValue)
        {
            return new ScalarValue(LogMaskingPolicy.Redacted);
        }

        switch (value)
        {
            case ScalarValue { Value: string text }:
            {
                var masked = LogMaskingPolicy.Mask(text);
                return masked == text ? value : new ScalarValue(masked);
            }

            case StructureValue structure:
            {
                var changed = false;
                var properties = new List<LogEventProperty>(structure.Properties.Count);
                foreach (var property in structure.Properties)
                {
                    var masked = Mask(property.Name, property.Value);
                    changed |= !ReferenceEquals(masked, property.Value);
                    properties.Add(ReferenceEquals(masked, property.Value) ? property : new LogEventProperty(property.Name, masked));
                }

                return changed ? new StructureValue(properties, structure.TypeTag) : value;
            }

            case SequenceValue sequence:
            {
                var changed = false;
                var elements = new List<LogEventPropertyValue>(sequence.Elements.Count);
                foreach (var element in sequence.Elements)
                {
                    var masked = Mask(propertyName: null, element);
                    changed |= !ReferenceEquals(masked, element);
                    elements.Add(masked);
                }

                return changed ? new SequenceValue(elements) : value;
            }

            case DictionaryValue dictionary:
            {
                var changed = false;
                var entries = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dictionary.Elements.Count);
                foreach (var (key, element) in dictionary.Elements)
                {
                    // O nome da entrada (ex.: "password") decide a supressão, como numa propriedade.
                    var masked = Mask(key.Value as string, element);
                    changed |= !ReferenceEquals(masked, element);
                    entries.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(key, masked));
                }

                return changed ? new DictionaryValue(entries) : value;
            }

            default:
                return value;
        }
    }
}
