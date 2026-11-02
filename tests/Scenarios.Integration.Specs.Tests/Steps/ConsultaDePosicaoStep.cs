namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>Spec consulta-de-posicao: posição em um instante, atualização, fechamentos paginados, validações e cache.</summary>
[Binding]
public sealed class ConsultaDePosicaoStep(SpecScenarioContext contexto)
{
    private string Conta => (string)contexto.Memoria["conta"];

    private DateTimeOffset Agora => contexto.Relogio.GetUtcNow();

    private static DateTimeOffset Dia(int mes, int dia, int hora = 10) => new(2026, mes, dia, hora, 0, 0, TimeSpan.Zero);

    /// <summary>O PostgreSQL guarda instantes em microssegundos: os instantes publicados já vão nessa precisão.</summary>
    private static DateTimeOffset EmMicrossegundos(DateTimeOffset instante) => new(instante.Ticks - (instante.Ticks % 10), instante.Offset);

    private static string Utc(DateTimeOffset instante) => instante.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    /// <summary>Conta com eventos sintéticos em datas passadas, que Lançamentos nunca aceitaria.</summary>
    private async Task ContaComMovimentosAsync(DateTimeOffset abertura, params (string Tipo, decimal Valor, DateTimeOffset Competencia)[] movimentos)
    {
        var conta = Guid.NewGuid().ToString();
        contexto.Memoria["conta"] = conta;
        await contexto.PublicarAsync("ContaAbertaV1", EventosSinteticos.ContaAberta(conta, contexto.Titular, abertura));
        for (var i = 0; i < movimentos.Length; i++)
        {
            await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(conta, i + 1, movimentos[i].Tipo, movimentos[i].Valor, movimentos[i].Competencia));
        }

        await contexto.EsperarProjecaoAsync(conta, movimentos.Length);
    }

    private async Task<string> ContaRealAsync()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.EsperarProjecaoAsync(conta, 0);
        return conta;
    }

    // Posição em um instante

    [Given(@"^a conta C com crédito de R\$ 100,00 às 10h e débito de R\$ 30,00 às 15h de 27/10/2026$")]
    public async Task CreditoEDebitoNoDia() =>
        await ContaComMovimentosAsync(Dia(10, 26), ("Credito", 100m, Dia(10, 27, 10)), ("Debito", 30m, Dia(10, 27, 15)));

    /// <summary>"(UTC)" informa o fuso; "sem informar o fuso horário" envia o instante sem ele.</summary>
    [When(@"^o titular consulta a posição de (\d{2}/\d{2}/\d{4}) às (\d{1,2})h( \(UTC\)| sem informar o fuso horário)$")]
    public async Task ConsultaNoInstante(string data, int hora, string fuso)
    {
        var instante = Linguagem.Instante(data, hora).ToString(fuso == " (UTC)" ? "yyyy-MM-dd'T'HH:mm:ss'Z'" : "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        if (!contexto.Memoria.ContainsKey("conta"))
        {
            await ContaRealAsync();
        }

        await contexto.ConsultarPosicaoAsync(Conta, consulta: $"?instante={instante}");
    }

    [Then(@"^o saldo é (R\$ [\d.,]+)$")]
    public void SaldoE(string saldo)
    {
        contexto.UltimaResposta.Status.Should().Be(HttpStatusCode.OK);
        contexto.UltimaResposta.Numero("saldo").Should().Be(Linguagem.Reais(saldo));
    }

    [Given(@"^a conta C com saldo aplicado de R\$ 70,00$")]
    public async Task SaldoSetenta()
    {
        var conta = await ContaRealAsync();
        await contexto.CreditarAsync(conta, 100m);
        await contexto.DebitarAsync(conta, 30m);
        await contexto.EsperarProjecaoAsync(conta, 2);
    }

    [When(@"^o titular consulta a posição sem informar o instante$")]
    [When(@"^o titular consulta a posição$")]
    public async Task ConsultaAgora() => await contexto.ConsultarPosicaoAsync(Conta);

    [When(@"^o titular consulta a posição de um instante anterior a qualquer movimentação da conta$")]
    public async Task ConsultaAntesDasMovimentacoes()
    {
        var conta = await ContaRealAsync();
        var antes = Agora;
        await contexto.CreditarAsync(conta, 50m);
        await contexto.EsperarProjecaoAsync(conta, 1);
        await contexto.ConsultarPosicaoAsync(conta, consulta: $"?instante={Utc(antes)}");
    }

    [When(@"^o titular consulta a posição de um instante no futuro$")]
    public async Task ConsultaNoFuturo()
    {
        var conta = await ContaRealAsync();
        await contexto.ConsultarPosicaoAsync(conta, consulta: $"?instante={Utc(Agora.AddHours(1))}");
    }

    // Atualização

    [Given(@"^a conta C com o primeiro e o segundo lançamento aplicados e o quarto pendente$")]
    public async Task SequenciaQuatroPendente()
    {
        var segunda = EmMicrossegundos(Agora.AddSeconds(-10));
        await ContaComMovimentosAsync(Agora.AddMinutes(-1), ("Credito", 100m, Agora.AddSeconds(-20)), ("Debito", 30m, segunda));
        contexto.Memoria["registro da segunda"] = segunda;
        await contexto.PublicarAsync("MovimentacaoRegistradaV1", EventosSinteticos.Movimentacao(Conta, 4, "Credito", 50m, Agora.AddSeconds(-5)));
        await Esperar.AteAsync(
            () =>
            {
                var id = new ContaDePosicao(Guid.Parse(Conta, CultureInfo.InvariantCulture));
                return contexto.PosicaoDbContext.Movimentos.AnyAsync(m => m.ContaId == id && m.Sequencia == 4);
            },
            "sequência 4 recebida");
    }

    [Then(@"^o saldo considera só o primeiro e o segundo lançamento$")]
    public void SoUmEDois() => contexto.UltimaResposta.Numero("saldo").Should().Be(70m);

    [Then(@"^a consulta informa que a posição está atualizada até o segundo lançamento, com o instante em que ele foi registrado$")]
    public void AtualizadoAteDaSegunda()
    {
        var atualizadoAte = contexto.UltimaResposta.Json.GetProperty("atualizadoAte");
        atualizadoAte.GetProperty("sequencia").ToString().Should().Be("2");
        atualizadoAte.GetProperty("instante").GetDateTimeOffset().Should().Be((DateTimeOffset)contexto.Memoria["registro da segunda"]);
    }

    // Fechamentos

    [Given(@"^a conta C com movimentações em 27/10/2026 e 29/10/2026$")]
    public async Task MovimentacoesEmDoisDias() =>
        await ContaComMovimentosAsync(Dia(10, 26), ("Credito", 100m, Dia(10, 27)), ("Debito", 30m, Dia(10, 29)));

    [When(@"^o titular consulta os fechamentos de 27/10/2026 a 31/10/2026$")]
    public async Task ConsultaFechamentos() => await contexto.ConsultarFechamentosAsync(Conta, "?de=2026-10-27&ate=2026-10-31");

    [Then(@"^a consulta traz só os fechamentos de 27/10/2026 e 29/10/2026, nessa ordem$")]
    public void SoOsDiasComMovimento()
    {
        contexto.UltimaResposta.Status.Should().Be(HttpStatusCode.OK);
        contexto.UltimaResposta.Json.GetProperty("itens").EnumerateArray().Select(f => f.GetProperty("data").GetString()).Should().Equal(["2026-10-27", "2026-10-29"]);
    }

    [Given(@"^a conta C com fechamentos em 3 dias do período$")]
    public async Task TresDias() =>
        await ContaComMovimentosAsync(Dia(10, 24), ("Credito", 10m, Dia(10, 25)), ("Credito", 10m, Dia(10, 26)), ("Credito", 10m, Dia(10, 27)));

    [When(@"^o titular consulta os fechamentos em páginas de 2 dias$")]
    public async Task PaginasDeDois()
    {
        contexto.Memoria["primeira página"] = await contexto.ConsultarFechamentosAsync(Conta, "?de=2026-10-20&ate=2026-10-28&tamanhoPagina=2&pagina=1");
        contexto.Memoria["segunda página"] = await contexto.ConsultarFechamentosAsync(Conta, "?de=2026-10-20&ate=2026-10-28&tamanhoPagina=2&pagina=2");
    }

    [Then(@"^a primeira página traz os 2 primeiros fechamentos e indica que há uma próxima página$")]
    public void PrimeiraPagina()
    {
        var pagina = ((Resposta)contexto.Memoria["primeira página"]).Json;
        pagina.GetProperty("itens").EnumerateArray().Select(f => f.GetProperty("data").GetString()).Should().Equal(["2026-10-25", "2026-10-26"]);
        pagina.GetProperty("temProximaPagina").GetBoolean().Should().BeTrue();
    }

    [Then(@"^a segunda página traz o terceiro fechamento e indica que não há outra$")]
    public void SegundaPagina()
    {
        var pagina = ((Resposta)contexto.Memoria["segunda página"]).Json;
        pagina.GetProperty("itens").EnumerateArray().Select(f => f.GetProperty("data").GetString()).Should().Equal(["2026-10-27"]);
        pagina.GetProperty("temProximaPagina").GetBoolean().Should().BeFalse();
    }

    [When(@"^o titular consulta os fechamentos de 29/10/2026 a 27/10/2026$")]
    public async Task PeriodoInvertido() => await contexto.ConsultarFechamentosAsync(await ContaRealAsync(), "?de=2026-10-29&ate=2026-10-27");

    [When(@"^o titular consulta os fechamentos de um período de 400 dias$")]
    public async Task PeriodoLongo() => await contexto.ConsultarFechamentosAsync(await ContaRealAsync(), "?de=2025-09-24&ate=2026-10-29");

    // Cache

    [Given(@"^que a aceleração das consultas está indisponível$")]
    public async Task CacheInacessivel()
    {
        await contexto.DerrubarCacheAsync();
        var conta = await ContaRealAsync();
        await contexto.CreditarAsync(conta, 42m);
        await contexto.EsperarProjecaoAsync(conta, 1);
    }

    [Then(@"^ele recebe o mesmo saldo de quando a aceleração está disponível$")]
    public async Task MesmoSaldoSemCache()
    {
        contexto.UltimaResposta.Status.Should().Be(HttpStatusCode.OK);
        contexto.UltimaResposta.Numero("saldo").Should().Be(await contexto.SaldoNoBancoAsync(Conta));
    }

    [Given(@"^que o titular já consultou a posição da conta C$")]
    public async Task ConsultaJaRespondida()
    {
        var conta = await ContaRealAsync();
        await contexto.CreditarAsync(conta, 100m);
        await contexto.EsperarProjecaoAsync(conta, 1);
        ((await contexto.ConsultarPosicaoAsync(conta)).Numero("saldo")).Should().Be(100m);
    }

    [When(@"^uma nova movimentação de C é aplicada e o titular consulta de novo$")]
    public async Task NovaMovimentacao()
    {
        await contexto.CreditarAsync(Conta, 20m);
        await contexto.EsperarProjecaoAsync(Conta, 2);
        await contexto.ConsultarPosicaoAsync(Conta);
    }

    [Then(@"^o saldo inclui a nova movimentação$")]
    public void SaldoComANova() => contexto.UltimaResposta.Numero("saldo").Should().Be(120m);
}
