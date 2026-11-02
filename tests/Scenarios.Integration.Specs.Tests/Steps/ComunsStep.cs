namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>
/// Passos que aparecem com o mesmo texto em mais de uma spec. Guardam a conta em <c>Memoria["conta"]</c> e o débito
/// em <c>Memoria["débito"]</c>, usados pelos passos seguintes de qualquer spec.
/// </summary>
[Binding]
public sealed class ComunsStep(SpecScenarioContext contexto)
{
    private string Conta => (string)contexto.Memoria["conta"];

    [When(@"^um cliente abre uma conta$")]
    public async Task AbrirConta()
    {
        var resposta = await contexto.AbrirContaRespostaAsync();
        if (resposta.Status == HttpStatusCode.Created)
        {
            contexto.Memoria["conta"] = resposta.Texto("contaId");
        }
    }

    [Given(@"^um débito registrado$")]
    public async Task DebitoRegistrado()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 100m);
        contexto.Memoria["débito"] = await contexto.DebitarAsync(conta, 30m);
    }

    [When(@"^o titular credita (R\$ [\d.,-]+)$")]
    public async Task Creditar(string valor) =>
        await contexto.LancarAsync(Conta, "creditos", new { valor = Linguagem.Reais(valor), descricao = "Crédito do cenário" });

    [When(@"^o titular debita (R\$ [\d.,-]+)$")]
    public async Task Debitar(string valor) =>
        await contexto.LancarAsync(Conta, "debitos", new { valor = Linguagem.Reais(valor), descricao = "Débito do cenário" });

    [When(@"^o titular estorna esse débito$")]
    public async Task EstornarDebito() => await contexto.EstornarAsync(Conta, (string)contexto.Memoria["débito"]);

    /// <summary>"o débito é recusado por saldo insuficiente", "a consulta é recusada por período inválido"...</summary>
    [Then(@"^(?:o|a) (?:crédito|débito|estorno|pedido|pedido de A|abertura|operação|consulta) é recusad[oa] (?:por|porque|como) ([^,]+)$")]
    public void Recusado(string motivo) => Linguagem.ConferirRecusa(contexto.UltimaResposta, motivo);

    [Then(@"^o (?:crédito|débito) é registrado$")]
    public void Registrado() => Linguagem.ConferirRegistro(contexto.UltimaResposta);
}
