namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>Spec contas: abertura, identificador não sequencial e abertura idempotente.</summary>
[Binding]
public sealed class ContasStep(SpecScenarioContext contexto)
{
    private const string Chave = "abertura-do-cenario";

    [Then(@"^a conta é aberta ativa, em reais, com saldo de R\$ 0,00 e sem limite$")]
    public void ContaAtivaZerada()
    {
        var resposta = contexto.UltimaResposta;
        resposta.Status.Should().Be(HttpStatusCode.Created);
        resposta.Texto("situacao").Should().Be("Ativa");
        resposta.Texto("moeda").Should().Be("BRL");
        resposta.Texto("saldoDisponivel").Should().Be("0.00");
        resposta.Texto("limite").Should().Be("0.00");
        resposta.Cabecalhos["Location"].Should().Be($"/lancamentos/contas/{resposta.Texto("contaId")}");
    }

    [Then(@"^o cliente é o titular da conta$")]
    public async Task ClienteEhOTitular()
    {
        var id = new ContaDeLancamentos(Guid.Parse(contexto.UltimaResposta.Texto("contaId"), CultureInfo.InvariantCulture));
        var conta = await contexto.LancamentosDbContext.Contas.SingleAsync(c => c.Id == id);
        conta.Titular.Valor.Should().Be(contexto.Titular);
    }

    [When(@"^um cliente abre uma conta pedindo um limite de R\$ 1\.000,00$")]
    public async Task AbrirComLimite() => await contexto.AbrirContaRespostaAsync(corpo: new { limite = 1000m });

    [Then(@"^a conta é aberta sem limite$")]
    public void LimiteIgnorado()
    {
        contexto.UltimaResposta.Status.Should().Be(HttpStatusCode.Created);
        contexto.UltimaResposta.Texto("limite").Should().Be("0.00");
    }

    [When(@"^um cliente abre duas contas, uma depois da outra$")]
    public async Task DuasContas()
    {
        contexto.Memoria["primeira"] = await contexto.AbrirContaAsync();
        contexto.Memoria["segunda"] = await contexto.AbrirContaAsync();
    }

    [Then(@"^o número de uma conta não permite deduzir o número da outra$")]
    public void IdentificadoresNaoSequenciais()
    {
        var primeira = Guid.Parse((string)contexto.Memoria["primeira"], CultureInfo.InvariantCulture).ToByteArray(bigEndian: true);
        var segunda = Guid.Parse((string)contexto.Memoria["segunda"], CultureInfo.InvariantCulture).ToByteArray(bigEndian: true);

        // A parte aleatória (os 8 últimos bytes) muda por inteiro; a diferença entre os dois não é um passo pequeno.
        var diferenca = System.Numerics.BigInteger.Abs(new System.Numerics.BigInteger(segunda, isUnsigned: true, isBigEndian: true) - new System.Numerics.BigInteger(primeira, isUnsigned: true, isBigEndian: true));
        diferenca.Should().BeGreaterThan(ulong.MaxValue, "Identificadores próximos demais: parecem sequenciais.");
        primeira[8..].Zip(segunda[8..]).Count(par => par.First != par.Second).Should().BeGreaterThanOrEqualTo(4, "A parte aleatória dos identificadores quase não mudou.");
    }

    [When(@"^um cliente repete o mesmo pedido de abertura de conta$")]
    public async Task RepetirAbertura()
    {
        contexto.Memoria["primeira resposta"] = await contexto.AbrirContaRespostaAsync(chave: Chave);
        await contexto.AbrirContaRespostaAsync(chave: Chave);
    }

    [Then(@"^ele recebe a mesma conta da primeira vez$")]
    public void MesmaResposta()
    {
        var primeira = (Resposta)contexto.Memoria["primeira resposta"];
        contexto.UltimaResposta.Status.Should().Be(primeira.Status);
        contexto.UltimaResposta.Texto("contaId").Should().Be(primeira.Texto("contaId"));
        Comparar.SemIdentificadoresDaRequisicao(contexto.UltimaResposta.Corpo).Should().Be(Comparar.SemIdentificadoresDaRequisicao(primeira.Corpo));
    }

    [Then(@"^só uma conta é aberta$")]
    public async Task UmaContaPorChave()
    {
        var titular = TitularDeLancamentos.Criar(contexto.Titular).Valor;
        var chave = ChaveIdempotencia.Criar(Chave).Valor;
        (await contexto.LancamentosDbContext.Contas.CountAsync(c => c.Titular == titular && c.ChaveAbertura == chave)).Should().Be(1);
    }

    [When(@"^os clientes A e B fazem pedidos de abertura com a mesma identificação de pedido$")]
    public async Task DoisTitularesMesmaChave()
    {
        contexto.Memoria["conta de A"] = await contexto.AbrirContaAsync(contexto.TitularDe("A"), Chave);
        contexto.Memoria["conta de B"] = await contexto.AbrirContaAsync(contexto.TitularDe("B"), Chave);
    }

    [Then(@"^cada um recebe a sua própria conta$")]
    public async Task CadaUmComASua()
    {
        var contaDeA = (string)contexto.Memoria["conta de A"];
        var contaDeB = (string)contexto.Memoria["conta de B"];
        contaDeB.Should().NotBe(contaDeA);
        foreach (var (conta, cliente) in new[] { (contaDeA, "A"), (contaDeB, "B") })
        {
            var id = new ContaDeLancamentos(Guid.Parse(conta, CultureInfo.InvariantCulture));
            (await contexto.LancamentosDbContext.Contas.SingleAsync(c => c.Id == id)).Titular.Valor.Should().Be(contexto.TitularDe(cliente));
        }
    }

    [When(@"^um cliente pede a abertura de uma conta sem identificar o pedido$")]
    public async Task AbrirSemChave() => await contexto.AbrirContaSemChaveAsync();
}
