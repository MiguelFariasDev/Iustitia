using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Lê as datas do DataJud, que NÃO usa um formato só.
///
/// Observado na API real (2026-09): <c>movimentos[].dataHora</c> vem em ISO 8601
/// ("2018-10-30T14:06:24.000Z"), mas <c>dataAjuizamento</c> vem compacto
/// ("20181029000000", yyyyMMddHHmmss). A documentação pública mostra ISO nos dois — o
/// desencontro só aparece consultando de verdade, e custou um CNJ_INVALID_RESPONSE em cima
/// de uma resposta 200 perfeitamente válida.
///
/// Data ilegível vira <c>null</c> em vez de exceção: perder a data de ajuizamento degrada o
/// auto-preenchimento; derrubar a consulta inteira por causa dela cancela o recurso.
/// </summary>
public sealed class DataJudDateTimeConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly string[] FormatosCompactos = ["yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd"];

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            return null;
        }

        // O caminho ISO primeiro: é o formato da maioria dos campos.
        if (reader.TryGetDateTimeOffset(out var iso))
        {
            return iso;
        }

        var texto = reader.GetString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        // Sem fuso na string compacta. Assume UTC, que é o fuso em que o DataJud publica os
        // demais campos — tratar como horário local daria uma data diferente por máquina.
        return DateTime.TryParseExact(
            texto, FormatosCompactos, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var compacta)
            ? new DateTimeOffset(compacta, TimeSpan.Zero)
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        // Só lemos do DataJud; a escrita existe para o contrato do conversor e para o cache,
        // que serializa os modelos já traduzidos.
        if (value is { } data)
        {
            writer.WriteStringValue(data);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
