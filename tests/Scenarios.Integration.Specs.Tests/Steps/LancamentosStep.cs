namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>Spec lancamentos: crédito, débito, estorno, validações, idempotência, concorrência e formato dos erros.</summary>
[Binding]
public sealed class LancamentosStep(SpecScenarioContext contexto)
{
    private string Conta => (string)contexto.Memoria["conta"];

    private async Task<string> ContaComSaldoAsync(decimal saldo)
    {
        var conta = await contexto.AbrirContaAsync();
        if (saldo > 0)
        {
            await contexto.CreditarAsync(conta, saldo);
        }

        contexto.Memoria["conta"] = conta;
        return conta;
    }

    private async Task<Resposta> CreditoAsync(object corpo, string? chave = null) => await contexto.LancarAsync(Conta, "creditos", corpo, chave: chave);

    private async Task GuardarEstadoAsync()
    {
        contexto.Memoria["saldo antes"] = await contexto.SaldoNoBancoAsync(Conta);
        contexto.Memoria["lançamentos antes"] = await contexto.ContarLancamentosAsync(Conta);
    }

    // Contexto

    [Given(@"^uma conta com saldo de (R\$ [\d.,]+)(?: e sem limite)?$")]
    public async Task ContaComSaldo(string saldo) => await ContaComSaldoAsync(Linguagem.Reais(saldo));

    [Given(@"^uma conta bloqueada$")]
    public async Task ContaBloqueada()
    {
        await ContaComSaldoAsync(0m);

        // Não há operação de bloqueio exposta: a situação é alterada direto no banco, como faria um backoffice.
        var id = new ContaDeLancamentos(Guid.Parse(Conta, CultureInfo.InvariantCulture));
        await contexto.LancamentosDbContext.Contas
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Situacao, SituacaoConta.Bloqueada));
    }

    // Registrar crédito e débito

    [Then(@"^o crédito de R\$ 100,00 é registrado como o primeiro lançamento da conta$")]
    public void CreditoRegistrado()
    {
        Linguagem.ConferirRegistro(contexto.UltimaResposta);
        contexto.UltimaResposta.Texto("tipo").Should().Be("Credito");
        contexto.UltimaResposta.Numero("valor").Should().Be(100.00m);
        contexto.UltimaResposta.Texto("sequencia").Should().Be("1");
    }

    [Then(@"^o saldo (?:passa a ser|volta a ser) (R\$ [\d.,]+)$")]
    public void SaldoPassaASer(string saldo) => contexto.UltimaResposta.Numero("saldoDisponivel").Should().Be(Linguagem.Reais(saldo));

    /// <summary>Usa a conta do contexto; sem contexto (validação do valor), abre uma conta sem saldo.</summary>
    [When(@"^o titular tenta (creditar|debitar) (R\$ [\d.,]+)$")]
    public async Task TentaLancar(string operacao, string valor)
    {
        if (!contexto.Memoria.ContainsKey("conta"))
        {
            await ContaComSaldoAsync(0m);
        }

        await contexto.LancarAsync(Conta, operacao == "creditar" ? "creditos" : "debitos", new { valor = Linguagem.Reais(valor), descricao = "Lançamento do cenário" });
    }

    [Then(@"^o saldo continua R\$ 100,00 e nenhum lançamento é registrado$")]
    public async Task SaldoIntacto()
    {
        (await contexto.SaldoNoBancoAsync(Conta)).Should().Be(100.00m);
        (await contexto.ContarLancamentosAsync(Conta)).Should().Be(1L);
    }

    // Valor, descrição e competência

    [When(@"^o titular tenta creditar R\$ 0,00 ou R\$ -10,00$")]
    public async Task ValorZeroOuNegativo()
    {
        await ContaComSaldoAsync(0m);
        Linguagem.ConferirRecusa(await CreditoAsync(new { valor = 0m, descricao = "Crédito" }), "valor inválido");
        await CreditoAsync(new { valor = -10.00m, descricao = "Crédito" });
    }

    [Then(@"^o crédito é recusado por valor inválido, sem arredondamento$")]
    public async Task NadaArredondado()
    {
        Linguagem.ConferirRecusa(contexto.UltimaResposta, "valor inválido");
        (await contexto.ContarLancamentosAsync(Conta)).Should().Be(0L);
    }

    [When(@"^o titular tenta creditar R\$ 10,00 com uma descrição de 201 caracteres$")]
    public async Task DescricaoLonga()
    {
        await ContaComSaldoAsync(0m);
        await CreditoAsync(new { valor = 10m, descricao = new string('a', 201) });
    }

    [When(@"^o titular credita R\$ 10,00 sem informar a data de competência$")]
    public async Task SemCompetencia()
    {
        await ContaComSaldoAsync(0m);
        await CreditoAsync(new { valor = 10m, descricao = "Crédito" });
    }

    [Then(@"^o crédito vale a partir do instante em que foi registrado$")]
    public void CompetenciaIgualAoRegistro()
    {
        Linguagem.ConferirRegistro(contexto.UltimaResposta);
        DateTimeOffset.Parse(contexto.UltimaResposta.Texto("dataCompetencia"), CultureInfo.InvariantCulture).Should().Be(DateTimeOffset.Parse(contexto.UltimaResposta.Texto("dataRegistro"), CultureInfo.InvariantCulture));
    }

    [When(@"^o titular tenta creditar R\$ 10,00 com competência daqui a uma hora$")]
    public async Task CompetenciaNoFuturo()
    {
        await ContaComSaldoAsync(0m);
        await CreditoAsync(new { valor = 10m, descricao = "Crédito", dataCompetencia = contexto.Relogio.GetUtcNow().AddHours(1) });
    }

    [When(@"^o titular tenta creditar R\$ 10,00 com competência no dia anterior$")]
    public async Task CompetenciaOntem()
    {
        await ContaComSaldoAsync(0m);
        var inicioDoDia = new DateTimeOffset(contexto.Relogio.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        await CreditoAsync(new { valor = 10m, descricao = "Crédito", dataCompetencia = inicioDoDia.AddMinutes(-1) });
    }

    // Sequência

    [When(@"^o titular credita R\$ 10,00, tenta debitar R\$ 50,00 e credita mais R\$ 10,00$")]
    public async Task CreditoDebitoRecusadoCredito()
    {
        contexto.Memoria["primeiro"] = await CreditoAsync(new { valor = 10m, descricao = "Crédito 1" });
        Linguagem.ConferirRecusa(await contexto.LancarAsync(Conta, "debitos", new { valor = 50m, descricao = "Débito" }), "saldo insuficiente");
        contexto.Memoria["segundo"] = await CreditoAsync(new { valor = 10m, descricao = "Crédito 2" });
    }

    [Then(@"^os dois créditos são o primeiro e o segundo lançamento da conta$")]
    public void SequenciasContiguas()
    {
        ((Resposta)contexto.Memoria["primeiro"]).Texto("sequencia").Should().Be("1");
        ((Resposta)contexto.Memoria["segundo"]).Texto("sequencia").Should().Be("2");
    }

    // Estorno

    [Given(@"^uma conta com saldo de R\$ 70,00 depois de um débito de R\$ 30,00$")]
    public async Task ContaDepoisDeUmDebito()
    {
        await ContaComSaldoAsync(100m);
        contexto.Memoria["débito"] = await contexto.DebitarAsync(Conta, 30m);
    }

    [Then(@"^o estorno de R\$ 30,00 é registrado$")]
    public void EstornoRegistrado()
    {
        Linguagem.ConferirRegistro(contexto.UltimaResposta);
        contexto.UltimaResposta.Texto("tipo").Should().Be("Estorno");
        contexto.UltimaResposta.Numero("valor").Should().Be(30.00m);
    }

    [Given(@"^uma conta sem limite que recebeu um crédito de R\$ 100,00 e ficou com saldo de R\$ 40,00$")]
    public async Task CreditoConsumido()
    {
        var conta = await ContaComSaldoAsync(0m);
        contexto.Memoria["crédito"] = await contexto.CreditarAsync(conta, 100m);
        await contexto.DebitarAsync(conta, 60m);
    }

    [When(@"^o titular tenta estornar esse crédito$")]
    public async Task EstornaCredito() => await contexto.EstornarAsync(Conta, (string)contexto.Memoria["crédito"]);

    [Given(@"^um débito já estornado$")]
    public async Task DebitoJaEstornado()
    {
        await ContaComSaldoAsync(100m);
        var debito = await contexto.DebitarAsync(Conta, 30m);
        contexto.Memoria["débito"] = debito;
        var estorno = await contexto.EstornarAsync(Conta, debito);
        Linguagem.ConferirRegistro(estorno);
        contexto.Memoria["estorno"] = estorno.Texto("lancamentoId");
    }

    [When(@"^o titular faz um novo pedido de estorno desse débito$")]
    public async Task SegundoEstorno()
    {
        contexto.Memoria["saldo antes"] = await contexto.SaldoNoBancoAsync(Conta);
        await contexto.EstornarAsync(Conta, (string)contexto.Memoria["débito"]);
    }

    [Then(@"^o saldo não muda$")]
    public async Task SaldoNaoMuda() => (await contexto.SaldoNoBancoAsync(Conta)).Should().Be((decimal)contexto.Memoria["saldo antes"]);

    [When(@"^o titular tenta estornar o estorno$")]
    public async Task EstornaEstorno() => await contexto.EstornarAsync(Conta, (string)contexto.Memoria["estorno"]);

    [Given(@"^que o titular tem as contas C1 e C2 e um débito registrado em C2$")]
    public async Task DuasContas()
    {
        contexto.Memoria["C1"] = await ContaComSaldoAsync(100m);
        var c2 = await contexto.AbrirContaAsync();
        await contexto.CreditarAsync(c2, 50m);
        contexto.Memoria["débito de C2"] = await contexto.DebitarAsync(c2, 10m);
    }

    [When(@"^ele tenta estornar esse débito pela conta C1$")]
    public async Task EstornaPelaOutraConta() => await contexto.EstornarAsync((string)contexto.Memoria["C1"], (string)contexto.Memoria["débito de C2"]);

    // Imutabilidade

    [When(@"^o titular tenta alterar ou excluir esse débito$")]
    public async Task AlteraLancamento()
    {
        await GuardarEstadoAsync();
        var respostas = new List<Resposta>();
        foreach (var metodo in new[] { HttpMethod.Delete, HttpMethod.Put, HttpMethod.Patch })
        {
            respostas.Add(await contexto.AlterarLancamentoAsync(metodo, Conta, (string)contexto.Memoria["débito"], new { valor = 1m }));
        }

        contexto.Memoria["respostas"] = respostas;
    }

    [Then(@"^o sistema não oferece essa possibilidade$")]
    public void NaoEncontradoOuNaoPermitido() =>
        ((List<Resposta>)contexto.Memoria["respostas"]).ToList().ForEach(r => r.Status.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed));

    [Then(@"^o lançamento e o saldo permanecem inalterados$")]
    public async Task LancamentoInalterado()
    {
        (await contexto.SaldoNoBancoAsync(Conta)).Should().Be((decimal)contexto.Memoria["saldo antes"]);
        (await contexto.ContarLancamentosAsync(Conta)).Should().Be((long)contexto.Memoria["lançamentos antes"]);
    }

    // Idempotência

    [When(@"^o titular repete o mesmo pedido de débito de R\$ 30,00, sem saber se o primeiro chegou$")]
    public async Task RepeteDebito()
    {
        var corpo = new { valor = 30m, descricao = "Conta de luz" };
        contexto.Memoria["original"] = await contexto.LancarAsync(Conta, "debitos", corpo, chave: "debito-1");
        await contexto.LancarAsync(Conta, "debitos", corpo, chave: "debito-1");
    }

    [Then(@"^ele recebe o mesmo débito da primeira vez$")]
    public void MesmaRespostaDaOriginal()
    {
        var original = (Resposta)contexto.Memoria["original"];
        contexto.UltimaResposta.Status.Should().Be(original.Status);
        contexto.UltimaResposta.Texto("lancamentoId").Should().Be(original.Texto("lancamentoId"));
    }

    [Then(@"^a conta foi debitada uma única vez e o saldo é R\$ 70,00$")]
    public async Task DebitadoUmaVez()
    {
        // O crédito inicial e um único débito.
        (await contexto.ContarLancamentosAsync(Conta)).Should().Be(2L);
        (await contexto.SaldoNoBancoAsync(Conta)).Should().Be(70m);
    }

    [Given(@"^um débito de R\$ 30,00 registrado$")]
    public async Task DebitoDeTrinta()
    {
        await ContaComSaldoAsync(100m);
        await contexto.LancarAsync(Conta, "debitos", new { valor = 30m, descricao = "Débito" }, chave: "debito-1");
    }

    [When(@"^o titular reaproveita o mesmo pedido para debitar R\$ 50,00$")]
    public async Task ChaveComOutroConteudo()
    {
        await GuardarEstadoAsync();
        await contexto.LancarAsync(Conta, "debitos", new { valor = 50m, descricao = "Débito" }, chave: "debito-1");
    }

    [Then(@"^nada muda$")]
    public async Task NadaMuda() => await LancamentoInalterado();

    [Given(@"^que um débito de R\$ 50,00 foi recusado por saldo insuficiente$")]
    public async Task DebitoRecusado()
    {
        await ContaComSaldoAsync(0m);
        Linguagem.ConferirRecusa(await contexto.LancarAsync(Conta, "debitos", new { valor = 50m, descricao = "Débito" }, chave: "debito-1"), "saldo insuficiente");
    }

    [When(@"^o titular recebe um crédito de R\$ 50,00 e repete o mesmo pedido de débito$")]
    public async Task RepeteDebitoRecusado()
    {
        await contexto.CreditarAsync(Conta, 50m);
        await contexto.LancarAsync(Conta, "debitos", new { valor = 50m, descricao = "Débito" }, chave: "debito-1");
    }

    [Then(@"^o débito é avaliado de novo e registrado, porque operações recusadas não ficam registradas$")]
    public async Task DebitoReavaliado()
    {
        Linguagem.ConferirRegistro(contexto.UltimaResposta);
        (await contexto.SaldoNoBancoAsync(Conta)).Should().Be(0m);
    }

    // Concorrência

    [When(@"^o titular faz 10 débitos de R\$ 10,00 ao mesmo tempo$")]
    public async Task DebitosSimultaneos()
    {
        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            contexto.LancarAsync(Conta, "debitos", new { valor = 10m, descricao = "Débito simultâneo" })));
        contexto.Memoria["respostas"] = respostas.ToList();
    }

    [Then(@"^exatamente 5 débitos são registrados e os outros 5 são recusados por saldo insuficiente$")]
    public void CincoAceitos()
    {
        var respostas = (List<Resposta>)contexto.Memoria["respostas"];
        respostas.Count(r => r.Status == HttpStatusCode.Created).Should().Be(5);
        respostas.Where(r => r.Status != HttpStatusCode.Created).ToList().ForEach(r => Linguagem.ConferirRecusa(r, "saldo insuficiente"));
    }

    [Then(@"^o saldo final é R\$ 0,00 e os débitos registrados ficam em sequência, sem lacunas$")]
    public async Task SaldoFinalZero()
    {
        (await contexto.SaldoNoBancoAsync(Conta)).Should().Be(0m);
        var sequencias = ((List<Resposta>)contexto.Memoria["respostas"])
            .Where(r => r.Status == HttpStatusCode.Created)
            .Select(r => long.Parse(r.Texto("sequencia"), CultureInfo.InvariantCulture))
            .Order()
            .ToList();
        sequencias.Should().Equal(Enumerable.Range(2, 5).Select(s => (long)s));
    }

    // Formato dos erros

    [When(@"^o titular envia um pedido de crédito ilegível$")]
    public async Task JsonMalformado()
    {
        await ContaComSaldoAsync(0m);
        await CreditoAsync(new StringContent("{ \"valor\": ", Encoding.UTF8, "application/json"));
    }

    [Then(@"^o pedido é recusado como inválido, com o motivo identificado$")]
    public void ProblemaDeRequisicaoInvalida()
    {
        Linguagem.ConferirRecusa(contexto.UltimaResposta, "inválido");
        contexto.UltimaResposta.Cabecalhos["Content-Type"].Should().StartWith("application/problem+json");
    }

    [When(@"^ocorre uma falha técnica imprevista durante um crédito$")]
    public async Task ErroTecnico()
    {
        await ContaComSaldoAsync(0m);

        // O banco de Lançamentos deixa de aceitar conexões no meio da operação: uma falha técnica real, não prevista.
        await contexto.TornarBancoDeLancamentosIndisponivelAsync();
        try
        {
            await CreditoAsync(new { valor = 10m, descricao = "Crédito" });
        }
        finally
        {
            await contexto.TornarBancoDeLancamentosDisponivelAsync();
        }
    }

    [Then(@"^o crédito é recusado por erro interno, sem revelar detalhes técnicos$")]
    public void ProblemaSemDetalhes()
    {
        contexto.UltimaResposta.Status.Should().Be(HttpStatusCode.InternalServerError);
        contexto.UltimaResposta.Cabecalhos["Content-Type"].Should().StartWith("application/problem+json");
        foreach (var vazamento in new[] { "exception", "   at ", "Npgsql", "stack", "database" })
        {
            contexto.UltimaResposta.Corpo.Should().NotContainEquivalentOf(vazamento);
        }
    }
}
