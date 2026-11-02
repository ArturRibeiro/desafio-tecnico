namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>
/// Spec projecao-da-posicao. Os cenários que dependem de uma ordem, de datas ou de conteúdo que Lançamentos nunca
/// produz publicam eventos sintéticos direto na exchange do contrato; os demais usam o fluxo real.
/// </summary>
[Binding]
public sealed class ProjecaoDaPosicaoStep(SpecScenarioContext contexto)
{
    private static readonly TimeSpan JanelaSemMudanca = TimeSpan.FromSeconds(3);

    private string Conta => (string)contexto.Memoria["conta"];

    private DateTimeOffset Agora => contexto.Relogio.GetUtcNow();

    private string Hoje => DateOnly.FromDateTime(Agora.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<string> ContaSinteticaAsync(bool publicarAbertura = true, DateTimeOffset? dataAbertura = null)
    {
        var conta = Guid.NewGuid().ToString();
        contexto.Memoria["conta"] = conta;
        if (publicarAbertura)
        {
            await contexto.PublicarAsync("ContaAbertaV1", EventosSinteticos.ContaAberta(conta, contexto.Titular, dataAbertura ?? Agora.AddMinutes(-1)));
            await contexto.EsperarProjecaoAsync(conta, 0);
        }

        return conta;
    }

    private async Task<decimal> SaldoNaPosicaoAsync()
    {
        var resposta = await contexto.ConsultarPosicaoAsync(Conta);
        resposta.Status.Should().Be(HttpStatusCode.OK);
        return resposta.Numero("saldo");
    }

    private async Task<long> SequenciaNaPosicaoAsync() =>
        long.Parse((await contexto.ConsultarPosicaoAsync(Conta)).Json.GetProperty("atualizadoAte").GetProperty("sequencia").ToString(), CultureInfo.InvariantCulture);

    private async Task<bool> MovimentoRecebidoAsync(string conta, long sequencia)
    {
        var id = new ContaDePosicao(Guid.Parse(conta, CultureInfo.InvariantCulture));
        return await contexto.PosicaoDbContext.Movimentos.AnyAsync(m => m.ContaId == id && m.Sequencia == sequencia);
    }

    private async Task<string> FechamentosAsync(string de, string ate)
    {
        var resposta = await contexto.ConsultarFechamentosAsync(Conta, $"?de={de}&ate={ate}");
        resposta.Status.Should().Be(HttpStatusCode.OK);
        return resposta.Json.GetProperty("itens").ToString();
    }

    // Titularidade projetada

    [Given(@"^que Lançamentos avisou a abertura da conta C do titular T$")]
    public async Task ContaAbertaComTitularT() => contexto.Memoria["conta"] = await contexto.AbrirContaAsync(contexto.TitularDe("T"));

    [When(@"^a Posição Consolidada processa o aviso$")]
    public async Task EventoProcessado() => await contexto.EsperarProjecaoAsync(Conta, 0);

    [Then(@"^a conta C passa a pertencer a T na Posição Consolidada$")]
    public async Task ContaDeT()
    {
        (await contexto.ConsultarPosicaoAsync(Conta, contexto.TitularDe("T"))).Status.Should().Be(HttpStatusCode.OK);
        (await contexto.ConsultarPosicaoAsync(Conta, contexto.TitularDe("outro"))).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Then(@"^o saldo de C é R\$ 0,00$")]
    public async Task SaldoZero() => ((await contexto.ConsultarPosicaoAsync(Conta, contexto.TitularDe("T"))).Numero("saldo")).Should().Be(0.00m);

    // Uma única vez

    [Given(@"^um crédito de R\$ 100,00, primeiro lançamento da conta C, já aplicado à posição$")]
    public async Task CreditoAplicado()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        await contexto.EsperarProjecaoAsync(conta, 1);
    }

    [When(@"^o aviso desse crédito chega de novo$")]
    public async Task MesmoEventoDeNovo()
    {
        var evento = contexto.Eventos.Da("MovimentacaoRegistradaV1", Conta).Single();

        // Mesmo evento (mesmo eventoId e sequência) numa mensagem nova: passa pelo Inbox e para na regra do Domínio.
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", JsonNode.Parse(evento.Mensagem.GetRawText())!.AsObject());
    }

    [Then(@"^o saldo de C continua R\$ 100,00$")]
    public async Task SaldoContinuaCem()
    {
        await Esperar.NuncaAsync(async () => await SaldoNaPosicaoAsync() != 100.00m, "saldo diferente de 100.00", JanelaSemMudanca);
        (await MovimentoRecebidoAsync(Conta, 1)).Should().BeTrue();
    }

    // Ordem

    [Given(@"^a conta C com o primeiro lançamento aplicado$")]
    public async Task SequenciaUmAplicada()
    {
        var conta = await ContaSinteticaAsync();
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(conta, 1, "Credito", 100m, Agora));
        await contexto.EsperarProjecaoAsync(conta, 1);
    }

    [When(@"^o terceiro lançamento chega antes do segundo$")]
    public async Task ChegaATres()
    {
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(Conta, 3, "Credito", 30m, Agora));
        await Esperar.AteAsync(() => MovimentoRecebidoAsync(Conta, 3), "sequência 3 recebida");
    }

    [Then(@"^o terceiro lançamento fica pendente e não altera a posição$")]
    public async Task TresPendente()
    {
        (await SequenciaNaPosicaoAsync()).Should().Be(1L);
        (await SaldoNaPosicaoAsync()).Should().Be(100m);
    }

    [Then(@"^quando o segundo lançamento chega, o segundo e o terceiro são aplicados nessa ordem$")]
    public async Task DoisETresAplicadas()
    {
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(Conta, 2, "Credito", 20m, Agora));
        await contexto.EsperarProjecaoAsync(Conta, 3);
        (await SaldoNaPosicaoAsync()).Should().Be(150m);
        (await SequenciaNaPosicaoAsync()).Should().Be(3L);
    }

    [Given(@"^que a Posição Consolidada ainda não tomou conhecimento da abertura da conta C$")]
    public async Task AberturaNaoProcessada() => await ContaSinteticaAsync(publicarAbertura: false);

    [When(@"^chega o primeiro lançamento de C$")]
    public async Task ChegaAMovimentacao()
    {
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(Conta, 1, "Credito", 40m, Agora));
        await Esperar.AteAsync(() => MovimentoRecebidoAsync(Conta, 1), "movimentação recebida");
    }

    [Then(@"^ele fica pendente$")]
    public async Task FicaPendente()
    {
        var id = new ContaDePosicao(Guid.Parse(Conta, CultureInfo.InvariantCulture));
        (await contexto.PosicaoDbContext.Titularidades.AnyAsync(t => t.ContaId == id)).Should().BeFalse();
        (await contexto.ConsultarPosicaoAsync(Conta)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Then(@"^é aplicado logo depois que a abertura de C é processada$")]
    public async Task AplicadaDepoisDaAbertura()
    {
        await contexto.PublicarAsync("ContaAbertaV1", EventosSinteticos.ContaAberta(Conta, contexto.Titular, Agora.AddMinutes(-1)));
        await contexto.EsperarProjecaoAsync(Conta, 1);
        (await SaldoNaPosicaoAsync()).Should().Be(40m);
    }

    // Fechamento diário

    [Given(@"^a conta C com crédito de R\$ 100,00 em 27/10/2026 e débito de R\$ 30,00 em 29/10/2026$")]
    public async Task DoisDias()
    {
        var conta = await ContaSinteticaAsync(dataAbertura: new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        contexto.Memoria["eventos"] = new[]
        {
            EventosSinteticos.Movimentacao(conta, 1, "Credito", 100m, new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero)),
            EventosSinteticos.Movimentacao(conta, 2, "Debito", 30m, new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero)),
        };
    }

    [When(@"^as duas movimentações são aplicadas$")]
    public async Task DoisEventosAplicados()
    {
        foreach (var evento in (JsonObject[])contexto.Memoria["eventos"])
        {
            await contexto.PublicarAsync("MovimentacaoRegistradaV1", evento);
        }

        await contexto.EsperarProjecaoAsync(Conta, 2);
    }

    [Then(@"^o fechamento de (\d{2}/\d{2}/\d{4}) tem abertura de (R\$ [\d.,]+), créditos de (R\$ [\d.,]+), débitos de (R\$ [\d.,]+) e fechamento de (R\$ [\d.,]+)$")]
    public async Task Fechamento(string data, string abertura, string creditos, string debitos, string fechamento)
    {
        var itens = JsonDocument.Parse(await FechamentosAsync("2026-10-27", "2026-10-29")).RootElement;
        var dia = itens.EnumerateArray().Single(f => f.GetProperty("data").GetString() == Linguagem.Data(data).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        decimal Valor(string campo) => decimal.Parse(dia.GetProperty(campo).ToString(), CultureInfo.InvariantCulture);
        Valor("saldoAbertura").Should().Be(Linguagem.Reais(abertura));
        Valor("totalCreditos").Should().Be(Linguagem.Reais(creditos));
        Valor("totalDebitos").Should().Be(Linguagem.Reais(debitos));
        Valor("saldoFechamento").Should().Be(Linguagem.Reais(fechamento));
    }

    [Given(@"^um débito de R\$ 30,00 aplicado à conta C$")]
    public async Task DebitoAplicado()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        contexto.Memoria["débito"] = await contexto.DebitarAsync(conta, 30m);
        await contexto.EsperarProjecaoAsync(conta, 2);
    }

    [When(@"^o estorno desse débito é aplicado$")]
    public async Task EstornoAplicado()
    {
        (await contexto.EstornarAsync(Conta, (string)contexto.Memoria["débito"])).Status.Should().Be(HttpStatusCode.Created);
        await contexto.EsperarProjecaoAsync(Conta, 3);
    }

    [Then(@"^o estorno soma R\$ 30,00 aos créditos do dia e o saldo volta ao valor anterior ao débito$")]
    public async Task EstornoPeloEfeito()
    {
        var dia = JsonDocument.Parse(await FechamentosAsync(Hoje, Hoje)).RootElement.EnumerateArray().Single();
        dia.GetProperty("totalCreditos").ToString().Should().Be("130.00");
        dia.GetProperty("totalDebitos").ToString().Should().Be("30.00");
        (await SaldoNaPosicaoAsync()).Should().Be(100.00m);
    }

    // Falhas isoladas

    [Given(@"^que a Posição Consolidada fica indisponível por um instante enquanto processa um crédito de R\$ 10,00$")]
    public async Task BancoIndisponivel()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.EsperarProjecaoAsync(conta, 0);
        await contexto.TornarBancoDePosicaoIndisponivelAsync();
        await contexto.CreditarAsync(conta, 10m);
    }

    [When(@"^ela volta antes de esgotadas as tentativas$")]
    public async Task BancoVolta()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        await contexto.TornarBancoDePosicaoDisponivelAsync();
    }

    [Then(@"^o crédito é aplicado uma única vez$")]
    public async Task AplicadoUmaVez()
    {
        await contexto.EsperarProjecaoAsync(Conta, 1);
        (await SaldoNaPosicaoAsync()).Should().Be(10m);
        (await MovimentoRecebidoAsync(Conta, 1)).Should().BeTrue();
    }

    [When(@"^chega uma movimentação de valor zero ou de um tipo desconhecido$")]
    public async Task MovimentacaoInvalida()
    {
        var valorZero = await ContaSinteticaAsync();
        var tipoDesconhecido = await ContaSinteticaAsync();
        contexto.Memoria["inválidas"] = new[] { valorZero, tipoDesconhecido };
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(valorZero, 1, "Credito", 0m, Agora));
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(tipoDesconhecido, 1, "Transferencia", 10m, Agora));
    }

    [Then(@"^ela é separada para análise, sem alterar a posição$")]
    public async Task VaiParaAFilaDeFalhas()
    {
        await Esperar.AteAsync(async () => await contexto.ContarMensagensComFalhaAsync("movimentacao-registrada") >= 2, "as duas movimentações na fila de falhas");
        foreach (var conta in (string[])contexto.Memoria["inválidas"])
        {
            (await MovimentoRecebidoAsync(conta, 1)).Should().BeFalse();
            contexto.Memoria["conta"] = conta;
            (await SequenciaNaPosicaoAsync()).Should().Be(0L);
        }
    }

    [Then(@"^as movimentações seguintes de outras contas continuam sendo aplicadas$")]
    public async Task OutrasContasSeguem()
    {
        var conta = await contexto.AbrirContaAsync();
        await contexto.CreditarAsync(conta, 10m);
        await contexto.EsperarProjecaoAsync(conta, 1);
    }

    // Retomada

    [Given(@"^que o titular registrou movimentações enquanto a Posição Consolidada estava parada$")]
    public async Task MovimentacoesComWorkerParado()
    {
        await contexto.PararWorkerAsync();
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        await contexto.DebitarAsync(conta, 30m);
        await contexto.CreditarAsync(conta, 5m);
    }

    [When(@"^a Posição Consolidada volta$")]
    public async Task ConsumoVolta() => await contexto.IniciarWorkerAsync();

    [Then(@"^todas essas movimentações são aplicadas e a posição fica igual ao saldo em Lançamentos$")]
    public async Task PosicaoIgualALancamentos()
    {
        await contexto.EsperarProjecaoAsync(Conta, 3);
        (await SaldoNaPosicaoAsync()).Should().Be(await contexto.SaldoNoBancoAsync(Conta));
    }

    // Reconstrução

    [Given(@"^fechamentos diários alterados indevidamente$")]
    public async Task FechamentosAlterados()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        await contexto.DebitarAsync(conta, 30m);
        await contexto.EsperarProjecaoAsync(conta, 2);
        contexto.Memoria["fechamentos corretos"] = await FechamentosAsync(Hoje, Hoje);

        var id = new ContaDePosicao(Guid.Parse(conta, CultureInfo.InvariantCulture));
        (await contexto.PosicaoDbContext.Fechamentos
            .Where(f => f.ContaId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.TotalCreditos, 999m).SetProperty(f => f.SaldoFechamento, 999m)))
            .Should().Be(1);
        (await FechamentosAsync(Hoje, Hoje)).Should().NotBe((string)contexto.Memoria["fechamentos corretos"]);
    }

    [When(@"^a posição é reconstruída$")]
    public async Task Reconstruir() => await contexto.ReconstruirAsync();

    [Then(@"^os fechamentos voltam a corresponder às movimentações recebidas$")]
    public async Task FechamentosCorrigidos() => (await FechamentosAsync(Hoje, Hoje)).Should().Be((string)contexto.Memoria["fechamentos corretos"]);

    [Given(@"^fechamentos já reconstruídos$")]
    public async Task JaReconstruidos()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        await contexto.EsperarProjecaoAsync(conta, 1);
        await contexto.ReconstruirAsync();
        contexto.Memoria["fechamentos reconstruídos"] = await FechamentosAsync(Hoje, Hoje);
    }

    [When(@"^a posição é reconstruída de novo$")]
    public async Task ReconstruirDeNovo() => await contexto.ReconstruirAsync();

    [Then(@"^os fechamentos não mudam$")]
    public async Task FechamentosIguais() => (await FechamentosAsync(Hoje, Hoje)).Should().Be((string)contexto.Memoria["fechamentos reconstruídos"]);
}
