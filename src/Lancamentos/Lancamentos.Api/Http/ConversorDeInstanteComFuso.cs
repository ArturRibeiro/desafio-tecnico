namespace Lancamentos.Api.Http;

/// <summary>
/// Exige o fuso no texto da data ("Z" ou "±hh:mm"). Sem ele, o servidor teria de assumir um fuso em silêncio,
/// e a mesma requisição significaria instantes diferentes conforme a máquina.
/// </summary>
internal sealed partial class ConversorDeInstanteComFuso : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        return texto is not null
            && ComFuso().IsMatch(texto)
            && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instante)
            ? instante
            : throw new JsonException("A data deve estar em ISO 8601 com fuso (por exemplo, 2026-10-27T14:32:10Z).");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ComFuso();
}
