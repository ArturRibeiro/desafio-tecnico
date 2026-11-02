namespace Plataforma.Hospedagem;

/// <summary>
/// Escreve o resultado das verificações expondo apenas o nome lógico e o estado de cada dependência.
/// Descrição, exceção e dados das verificações são descartados de propósito: podem conter
/// hosts, connection strings ou credenciais.
/// </summary>
public static class EscritorDeSaude
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static Task EscreverAsync(HttpContext contexto, HealthReport relatorio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relatorio);

        var corpo = new RespostaDeSaude(
            relatorio.Status.ToString(),
            relatorio.Entries
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => new EstadoDeDependencia(e.Key, e.Value.Status.ToString()))
                .ToList());

        contexto.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(contexto.Response.Body, corpo, Opcoes, contexto.RequestAborted);
    }

    private sealed record RespostaDeSaude(string Estado, IReadOnlyList<EstadoDeDependencia> Dependencias);

    private sealed record EstadoDeDependencia(string Nome, string Estado);
}
