namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>Resposta HTTP já lida, para os passos THEN conferirem status, cabeçalhos e corpo.</summary>
public sealed record Resposta(HttpStatusCode Status, string Corpo, IReadOnlyDictionary<string, string> Cabecalhos)
{
    public JsonElement Json => JsonDocument.Parse(Corpo).RootElement.Clone();

    public string? Codigo => Json.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;

    public static async Task<Resposta> LerAsync(HttpResponseMessage mensagem)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var cabecalhos = mensagem.Headers.Concat(mensagem.Content.Headers)
            .ToDictionary(c => c.Key, c => string.Join(',', c.Value), StringComparer.OrdinalIgnoreCase);
        return new Resposta(mensagem.StatusCode, await mensagem.Content.ReadAsStringAsync(), cabecalhos);
    }

    public string Texto(string propriedade) => Json.GetProperty(propriedade).ToString();

    public decimal Numero(string propriedade) => Json.GetProperty(propriedade).GetDecimal();
}
