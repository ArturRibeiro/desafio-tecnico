namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>
/// Tradução do vocabulário de negócio dos cenários para o que os passos enviam e conferem: valores em reais, datas no
/// formato brasileiro e o motivo de cada recusa, com o status e o código de erro da API.
/// </summary>
public static class Linguagem
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Dictionary<string, (HttpStatusCode Status, string Codigo)> Recusas = new(StringComparer.Ordinal)
    {
        ["saldo insuficiente"] = (HttpStatusCode.UnprocessableEntity, "saldo-insuficiente"),
        ["a conta não está ativa"] = (HttpStatusCode.UnprocessableEntity, "conta-inativa"),
        ["valor inválido"] = (HttpStatusCode.BadRequest, "valor-invalido"),
        ["descrição inválida"] = (HttpStatusCode.BadRequest, "descricao-invalida"),
        ["data de competência inválida"] = (HttpStatusCode.UnprocessableEntity, "data-competencia-invalida"),
        ["o lançamento já foi estornado"] = (HttpStatusCode.Conflict, "lancamento-ja-estornado"),
        ["um estorno não pode ser estornado"] = (HttpStatusCode.UnprocessableEntity, "estorno-nao-estornavel"),
        ["o lançamento não foi encontrado"] = (HttpStatusCode.NotFound, "lancamento-nao-encontrado"),
        ["a conta não foi encontrada"] = (HttpStatusCode.NotFound, "conta-nao-encontrada"),
        ["conflito com o pedido original"] = (HttpStatusCode.Conflict, "chave-idempotencia-conflitante"),
        ["falta da identificação do pedido"] = (HttpStatusCode.BadRequest, "chave-idempotencia-obrigatoria"),
        ["inválido"] = (HttpStatusCode.BadRequest, "requisicao-invalida"),
        ["instante inválido"] = (HttpStatusCode.BadRequest, "instante-invalido"),
        ["período inválido"] = (HttpStatusCode.BadRequest, "periodo-invalido"),
    };

    /// <summary>"R$ 1.000,00" ou "1.000,00" → 1000.00m. Mantém todas as casas: "10,005" → 10.005m.</summary>
    public static decimal Reais(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return decimal.Parse(texto.Replace("R$", string.Empty, StringComparison.Ordinal).Trim(), NumberStyles.Number, PtBr);
    }

    /// <summary>"27/10/2026" → 2026-10-27.</summary>
    public static DateOnly Data(string texto) => DateOnly.ParseExact(texto, "dd/MM/yyyy", PtBr);

    /// <summary>"27/10/2026" e "10h" → 2026-10-27T10:00:00Z.</summary>
    public static DateTimeOffset Instante(string data, int hora) => new(Data(data).ToDateTime(new TimeOnly(hora, 0)), TimeSpan.Zero);

    /// <summary>Confere que a resposta é a recusa descrita pelo <paramref name="motivo"/> do cenário.</summary>
    public static void ConferirRecusa(Resposta resposta, string motivo)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        Recusas.TryGetValue(motivo, out var esperado).Should().BeTrue($"Motivo de recusa sem tradução: \"{motivo}\".");
        resposta.Status.Should().Be(esperado.Status, $"Recusa por {motivo}: esperado {(int)esperado.Status}, veio {(int)resposta.Status}: {resposta.Corpo}");
        resposta.Codigo.Should().Be(esperado.Codigo);
    }

    /// <summary>Confere que a operação foi aceita e registrada (<c>201</c>).</summary>
    public static void ConferirRegistro(Resposta resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        resposta.Status.Should().Be(HttpStatusCode.Created, $"Esperado registro (201), veio {(int)resposta.Status}: {resposta.Corpo}");
    }
}
