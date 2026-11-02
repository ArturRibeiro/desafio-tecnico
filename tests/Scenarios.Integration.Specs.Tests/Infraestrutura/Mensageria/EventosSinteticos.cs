namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Mensageria;

/// <summary>Corpo dos contratos ContaAbertaV1 e MovimentacaoRegistradaV1, com os mesmos campos que Lançamentos publica.</summary>
public static class EventosSinteticos
{
    public static JsonObject ContaAberta(string conta, string titular, DateTimeOffset dataAbertura) => new()
    {
        ["tipoEvento"] = "ContaAberta",
        ["versao"] = 1,
        ["eventoId"] = Guid.NewGuid().ToString(),
        ["contaId"] = conta,
        ["titularId"] = titular,
        ["sequencia"] = 0,
        ["moeda"] = "BRL",
        ["dataAbertura"] = dataAbertura.ToString("O", CultureInfo.InvariantCulture),
        ["correlacaoId"] = Guid.NewGuid().ToString(),
    };

    public static JsonObject Movimentacao(
        string conta,
        long sequencia,
        string tipo,
        decimal valor,
        DateTimeOffset competencia,
        string? lancamento = null,
        string? estornado = null) => new()
    {
        ["tipoEvento"] = "MovimentacaoRegistrada",
        ["versao"] = 1,
        ["eventoId"] = Guid.NewGuid().ToString(),
        ["contaId"] = conta,
        ["lancamentoId"] = lancamento ?? Guid.NewGuid().ToString(),
        ["sequencia"] = sequencia,
        ["tipo"] = tipo,
        // Como o MassTransit publica: decimal em texto.
        ["valor"] = valor.ToString(CultureInfo.InvariantCulture),
        ["moeda"] = "BRL",
        ["dataCompetencia"] = competencia.ToString("O", CultureInfo.InvariantCulture),
        ["dataRegistro"] = competencia.ToString("O", CultureInfo.InvariantCulture),
        ["lancamentoEstornadoId"] = estornado,
        ["correlacaoId"] = Guid.NewGuid().ToString(),
    };
}
