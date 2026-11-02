namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>Spec titularidade: cada cliente só opera e consulta as próprias contas, sem revelar as dos outros.</summary>
[Binding]
public sealed class TitularidadeStep(SpecScenarioContext contexto)
{
    private static readonly TimeSpan JanelaSemEvento = TimeSpan.FromSeconds(3);

    [When(@"^o cliente A tenta creditar R\$ 50,00 na conta do cliente B$")]
    public async Task CreditoNaContaDeB()
    {
        var conta = await contexto.AbrirContaAsync(contexto.TitularDe("B"));
        contexto.Memoria["conta"] = conta;
        await Esperar.AteAsync(() => Task.FromResult(contexto.Eventos.Da("ContaAbertaV1", conta).Count == 1), "abertura da conta publicada");
        contexto.Memoria["saldo antes"] = await contexto.SaldoNoBancoAsync(conta);
        await contexto.LancarAsync(conta, "creditos", new { valor = 50m, descricao = "Crédito indevido" }, contexto.TitularDe("A"));
    }

    [Then(@"^nenhum lançamento é registrado$")]
    public async Task NenhumLancamento() => (await contexto.ContarLancamentosAsync((string)contexto.Memoria["conta"])).Should().Be(0L);

    [Then(@"^o saldo da conta de B não muda$")]
    public async Task SaldoNaoMuda() => (await contexto.SaldoNoBancoAsync((string)contexto.Memoria["conta"])).Should().Be((decimal)contexto.Memoria["saldo antes"]);

    [Then(@"^a Posição Consolidada não é avisada de nenhuma movimentação$")]
    public async Task NenhumEvento()
    {
        var conta = (string)contexto.Memoria["conta"];
        await Esperar.NuncaAsync(() => Task.FromResult(contexto.Eventos.Da("MovimentacaoRegistradaV1", conta).Count > 0), "movimentação publicada", JanelaSemEvento);
    }

    [When(@"^o cliente A tenta operar a conta do cliente B$")]
    public async Task OperaContaDeB()
    {
        var conta = await contexto.AbrirContaAsync(contexto.TitularDe("B"));
        await contexto.LancarAsync(conta, "creditos", new { valor = 10m, descricao = "Crédito indevido" }, contexto.TitularDe("A"));
    }

    [When(@"^o cliente tenta operar uma conta que não existe$")]
    public async Task OperaContaInexistente()
    {
        await OperaContaDeB();
        contexto.Memoria["resposta da conta de outro titular"] = contexto.UltimaResposta;
        await contexto.LancarAsync(Guid.NewGuid().ToString(), "creditos", new { valor = 10m, descricao = "Crédito indevido" }, contexto.TitularDe("A"));
    }

    [Then(@"^a operação é recusada porque a conta não foi encontrada, exatamente como na conta de outro titular$")]
    public void IdenticaAOutroTitular()
    {
        Linguagem.ConferirRecusa(contexto.UltimaResposta, "a conta não foi encontrada");
        var outroTitular = (Resposta)contexto.Memoria["resposta da conta de outro titular"];
        Comparar.SemIdentificadoresDaRequisicao(contexto.UltimaResposta.Corpo).Should().Be(Comparar.SemIdentificadoresDaRequisicao(outroTitular.Corpo));
    }

    [Given(@"^que o cliente B debitou R\$ 30,00 da sua conta$")]
    public async Task BDebitaComK1()
    {
        var conta = await contexto.AbrirContaAsync(contexto.TitularDe("B"));
        await contexto.CreditarAsync(conta, 50m, contexto.TitularDe("B"));
        contexto.Memoria["conta"] = conta;
        contexto.Memoria["débito de B"] = await contexto.DebitarAsync(conta, 30m, contexto.TitularDe("B"), "k1");
    }

    [When(@"^o cliente A repete o mesmo pedido na conta de B$")]
    public async Task AUsaK1NaContaDeB() =>
        await contexto.LancarAsync((string)contexto.Memoria["conta"], "debitos", new { valor = 30m, descricao = "Débito do cenário" }, contexto.TitularDe("A"), "k1");

    [Then(@"^A não recebe nenhum dado do débito de B$")]
    public void SemDadosDeB()
    {
        contexto.UltimaResposta.Corpo.Should().NotContainEquivalentOf((string)contexto.Memoria["débito de B"]);
        contexto.UltimaResposta.Corpo.Should().NotContain("lancamentoId");
    }

    [Given(@"^a conta C do cliente B$")]
    public async Task ContaCDeB()
    {
        var conta = await contexto.AbrirContaAsync(contexto.TitularDe("B"));
        contexto.Memoria["C"] = conta;
        await contexto.EsperarProjecaoAsync(conta, 0, contexto.TitularDe("B"));
    }

    [When(@"^o cliente A consulta a posição de C$")]
    public async Task AConsultaC() => await contexto.ConsultarPosicaoAsync((string)contexto.Memoria["C"], contexto.TitularDe("A"));

    [Then(@"^a recusa é idêntica à da consulta de uma conta que não existe$")]
    public async Task IgualAInexistente()
    {
        var deOutroTitular = contexto.UltimaResposta;
        var inexistente = await contexto.ConsultarPosicaoAsync(Guid.NewGuid().ToString(), contexto.TitularDe("A"));
        Comparar.SemIdentificadoresDaRequisicao(deOutroTitular.Corpo).Should().Be(Comparar.SemIdentificadoresDaRequisicao(inexistente.Corpo));
    }

    [Given(@"^que o cliente acabou de abrir a conta C e a Posição Consolidada ainda não tomou conhecimento da abertura$")]
    public async Task ContaAbertaSemProjecao()
    {
        await contexto.PararWorkerAsync();
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["C"] = conta;
        await Esperar.AteAsync(() => Task.FromResult(contexto.Eventos.Da("ContaAbertaV1", conta).Count == 1), "abertura da conta publicada");
    }

    [When(@"^ele consulta a posição de C$")]
    public async Task ConsultaC() => await contexto.ConsultarPosicaoAsync((string)contexto.Memoria["C"]);

    [Then(@"^depois que a Posição Consolidada toma conhecimento da abertura, a mesma consulta mostra o saldo da conta$")]
    public async Task DepoisDoProcessamento()
    {
        await contexto.IniciarWorkerAsync();
        await Esperar.AteAsync(async () => (await contexto.ConsultarPosicaoAsync((string)contexto.Memoria["C"])).Status == HttpStatusCode.OK, "posição da conta C disponível");
    }
}
