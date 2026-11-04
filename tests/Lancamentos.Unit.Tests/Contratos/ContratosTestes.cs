namespace Lancamentos.Unit.Tests.Contratos;

/// <summary>
/// O contrato serializado tem exatamente os campos documentados no DOMINIO.md, seção 7.
/// Se o código e a documentação divergirem, este teste falha.
/// </summary>
public sealed partial class ContratosTestes
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Evento ContaAberta v1 tem exatamente os campos documentados")]
    public void ContaAbertaV1_TemOsCamposDocumentados()
    {
        var evento = new ContaAbertaV1
        {
            EventoId = Guid.NewGuid(),
            ContaId = Guid.NewGuid(),
            TitularId = "auth0|abc",
            Moeda = "BRL",
            DataAbertura = DateTimeOffset.UtcNow,
            CorrelacaoId = "c-1",
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(evento, Web));

        Campos(json).Should().Equal(CamposDocumentados("ContaAberta"));
        json.RootElement.GetProperty("sequencia").GetInt64().Should().Be(0);
        json.RootElement.GetProperty("tipoEvento").GetString().Should().Be("ContaAberta");
        json.RootElement.GetProperty("versao").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "Evento MovimentacaoRegistrada v1 tem os campos documentados, sem titular nem descrição")]
    public void MovimentacaoRegistradaV1_TemOsCamposDocumentadosSemTitularNemDescricao()
    {
        var evento = new MovimentacaoRegistradaV1
        {
            EventoId = Guid.NewGuid(),
            ContaId = Guid.NewGuid(),
            LancamentoId = Guid.NewGuid(),
            Sequencia = 42,
            Tipo = "Debito",
            Valor = 150.00m,
            Moeda = "BRL",
            DataCompetencia = DateTimeOffset.UtcNow,
            DataRegistro = DateTimeOffset.UtcNow,
            SaldoDisponivelApos = 70.00m,
            CorrelacaoId = "c-1",
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(evento, Web));
        var campos = Campos(json);

        campos.Should().Equal(CamposDocumentados("MovimentacaoRegistrada"));
        json.RootElement.GetProperty("saldoDisponivelApos").GetDecimal().Should().Be(70.00m);
        campos.Should().NotContain("titularId");
        campos.Should().NotContain("descricao");
    }

    private static SortedSet<string> Campos(JsonDocument json) =>
        new(json.RootElement.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);

    /// <summary>Lê, no DOMINIO.md, o exemplo JSON do evento e devolve os nomes dos campos.</summary>
    private static SortedSet<string> CamposDocumentados(string tipoEvento)
    {
        var dominio = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "documentacao", "DOMINIO.md"));
        var exemplo = BlocosJson().Matches(dominio)
            .Select(m => m.Groups[1].Value)
            .Single(b => b.Contains($"\"tipoEvento\": \"{tipoEvento}\"", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(exemplo);
        return Campos(json);
    }

    private static string RaizDoRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "DesafioTecnico.slnx")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    [GeneratedRegex("```json\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex BlocosJson();
}
