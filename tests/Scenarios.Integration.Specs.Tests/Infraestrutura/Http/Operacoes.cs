namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>Operações de negócio pelas APIs, usadas pelos passos para montar o contexto e agir.</summary>
public static class Operacoes
{
    /// <summary>Titular de um cliente nomeado na spec ("A", "B"), único por cenário.</summary>
    public static string TitularDe(this SpecScenarioContext contexto, string cliente)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return $"{contexto.Titular}-{cliente}";
    }

    public static async Task<Resposta> AbrirContaRespostaAsync(this SpecScenarioContext contexto, string? titular = null, string? chave = null, object? corpo = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var api = contexto.Lancamentos;
        return await contexto.EnviarAsync(api, HttpMethod.Post, "/lancamentos/contas", corpo, chave ?? NovaChave(), contexto.Token(titular));
    }

    /// <summary>Pedido de abertura sem a Idempotency-Key.</summary>
    public static async Task<Resposta> AbrirContaSemChaveAsync(this SpecScenarioContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return await contexto.EnviarAsync(contexto.Lancamentos, HttpMethod.Post, "/lancamentos/contas");
    }

    public static async Task<string> AbrirContaAsync(this SpecScenarioContext contexto, string? titular = null, string? chave = null)
    {
        var resposta = await contexto.AbrirContaRespostaAsync(titular, chave);
        resposta.Status.Should().Be(HttpStatusCode.Created);
        return resposta.Texto("contaId");
    }

    /// <param name="operacao">"creditos" ou "debitos".</param>
    public static async Task<Resposta> LancarAsync(
        this SpecScenarioContext contexto,
        string conta,
        string operacao,
        object corpo,
        string? titular = null,
        string? chave = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var api = contexto.Lancamentos;
        return await contexto.EnviarAsync(api, HttpMethod.Post, $"/lancamentos/contas/{conta}/{operacao}", corpo, chave ?? NovaChave(), contexto.Token(titular));
    }

    public static async Task<string> CreditarAsync(this SpecScenarioContext contexto, string conta, decimal valor, string? titular = null, string? chave = null) =>
        await LancamentoCriadoAsync(contexto.LancarAsync(conta, "creditos", new { valor, descricao = "Crédito do cenário" }, titular, chave));

    public static async Task<string> DebitarAsync(this SpecScenarioContext contexto, string conta, decimal valor, string? titular = null, string? chave = null) =>
        await LancamentoCriadoAsync(contexto.LancarAsync(conta, "debitos", new { valor, descricao = "Débito do cenário" }, titular, chave));

    public static async Task<Resposta> EstornarAsync(this SpecScenarioContext contexto, string conta, string lancamento, object? corpo = null, string? titular = null, string? chave = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var api = contexto.Lancamentos;
        return await contexto.EnviarAsync(api, HttpMethod.Post, $"/lancamentos/contas/{conta}/lancamentos/{lancamento}/estornos", corpo, chave ?? NovaChave(), contexto.Token(titular));
    }

    /// <summary>Tenta alterar ou excluir um lançamento com <paramref name="metodo"/> (DELETE, PUT, PATCH).</summary>
    public static async Task<Resposta> AlterarLancamentoAsync(this SpecScenarioContext contexto, HttpMethod metodo, string conta, string lancamento, object? corpo = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return await contexto.EnviarAsync(contexto.Lancamentos, metodo, $"/lancamentos/contas/{conta}/lancamentos/{lancamento}", corpo, NovaChave());
    }

    public static async Task<Resposta> ConsultarPosicaoAsync(this SpecScenarioContext contexto, string conta, string? titular = null, string? consulta = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var api = contexto.Posicao;
        return await contexto.EnviarAsync(api, HttpMethod.Get, $"/posicao/contas/{conta}{consulta}", token: contexto.Token(titular));
    }

    public static async Task<Resposta> ConsultarFechamentosAsync(this SpecScenarioContext contexto, string conta, string consulta, string? titular = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var api = contexto.Posicao;
        return await contexto.EnviarAsync(api, HttpMethod.Get, $"/posicao/contas/{conta}/fechamentos{consulta}", token: contexto.Token(titular));
    }

    /// <summary>Espera a Posição aplicar a conta até a <paramref name="sequencia"/> (0 = só a abertura).</summary>
    public static async Task EsperarProjecaoAsync(this SpecScenarioContext contexto, string conta, long sequencia, string? titular = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        await Esperar.AteAsync(
            async () =>
            {
                var id = new ContaDePosicao(Guid.Parse(conta, CultureInfo.InvariantCulture));
                var aplicada = await contexto.PosicaoDbContext.Titularidades
                    .Where(t => t.ContaId == id)
                    .Select(t => (long?)t.UltimaSequenciaAplicada)
                    .SingleOrDefaultAsync();
                return aplicada >= sequencia;
            },
            $"Posição da conta {conta} até a sequência {sequencia}");
    }

    public static async Task<long> ContarLancamentosAsync(this SpecScenarioContext contexto, string conta)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var id = new ContaDeLancamentos(Guid.Parse(conta, CultureInfo.InvariantCulture));
        return await contexto.LancamentosDbContext.Lancamentos.LongCountAsync(l => l.ContaId == id);
    }

    public static async Task<decimal> SaldoNoBancoAsync(this SpecScenarioContext contexto, string conta)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var id = new ContaDeLancamentos(Guid.Parse(conta, CultureInfo.InvariantCulture));
        return await contexto.LancamentosDbContext.Contas.Where(c => c.Id == id).Select(c => c.SaldoDisponivel).SingleAsync();
    }

    public static string NovaChave() => Guid.NewGuid().ToString();

    private static async Task<string> LancamentoCriadoAsync(Task<Resposta> envio)
    {
        var resposta = await envio;
        resposta.Status.Should().Be(HttpStatusCode.Created, $"Esperado 201, veio {(int)resposta.Status}: {resposta.Corpo}");
        return resposta.Texto("lancamentoId");
    }
}
