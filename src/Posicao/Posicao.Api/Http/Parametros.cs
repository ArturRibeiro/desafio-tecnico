namespace Posicao.Api.Http;

/// <summary>Leitura dos parâmetros de consulta, sem fuso implícito nem formatos dependentes de cultura.</summary>
internal static partial class Parametros
{
    /// <summary>
    /// Instante em ISO 8601 com fuso ("Z" ou "±hh:mm"). Sem o fuso, o servidor teria de assumir um em silêncio.
    /// Ausente, vale o momento da consulta.
    /// </summary>
    public static Resultado<DateTimeOffset?> Instante(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return Resultado.Ok<DateTimeOffset?>(null);
        }

        return ComFuso().IsMatch(texto) && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instante)
            ? Resultado.Ok<DateTimeOffset?>(instante)
            : Resultado.Falhar<DateTimeOffset?>(CodigosDeFalha.InstanteInvalido, "O instante deve estar em ISO 8601 com fuso (por exemplo, 2026-10-27T14:32:10Z).");
    }

    /// <summary>Data yyyy-MM-dd; nula se ausente ou fora do formato (o caso de uso responde periodo-invalido).</summary>
    public static DateOnly? Data(string? texto) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;

    /// <summary>Inteiro; ausente, vale o padrão; fora do formato, vira 0, que o caso de uso recusa.</summary>
    public static int Inteiro(string? texto, int padrao) =>
        string.IsNullOrEmpty(texto) ? padrao : int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) ? valor : 0;

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ComFuso();
}
