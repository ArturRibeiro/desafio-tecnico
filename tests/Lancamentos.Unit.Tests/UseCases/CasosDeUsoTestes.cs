namespace Lancamentos.Unit.Tests.UseCases;

public sealed class CasosDeUsoTestes
{
    private static readonly TitularId OutroTitular = TitularId.Criar("cliente-b").Valor;

    private readonly Banco banco = new();
    private readonly RelogioFixo relogio = new(Agora);

    [Fact(DisplayName = "Abertura de conta repetida com a mesma chave devolve a mesma conta")]
    public async Task AbrirConta_RepetidaComAMesmaChave_DevolveAMesmaConta()
    {
        var abrir = new AbrirContaHandler(banco, banco, relogio);
        var comando = new AbrirContaCommand(Titular, Chave("abre-1"));

        var primeira = await abrir.HandleAsync(comando, Ct);
        var segunda = await abrir.HandleAsync(comando, Ct);

        segunda.Valor.ContaId.Should().Be(primeira.Valor.ContaId);
        banco.Contas.Should().ContainSingle();
    }

    [Fact(DisplayName = "Abertura com a mesma chave por outro titular abre outra conta")]
    public async Task AbrirConta_MesmaChavePorOutroTitular_AbreOutraConta()
    {
        var abrir = new AbrirContaHandler(banco, banco, relogio);

        var a = await abrir.HandleAsync(new AbrirContaCommand(Titular, Chave("abre-1")), Ct);
        var b = await abrir.HandleAsync(new AbrirContaCommand(OutroTitular, Chave("abre-1")), Ct);

        b.Valor.ContaId.Should().NotBe(a.Valor.ContaId);
    }

    [Fact(DisplayName = "Titularidade da conta é verificada antes da chave de idempotência")]
    public async Task Titularidade_EhVerificadaAntesDaChave()
    {
        var conta = await AbrirAsync();
        await Debitar(conta, Titular, 0m, "k1");
        banco.Chamadas.Clear();

        var resultado = await new RegistrarCreditoHandler(banco, banco, banco, relogio)
            .HandleAsync(Comando(conta, OutroTitular, 10m, "k1"), Ct);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ContaNaoEncontrada);
        banco.Chamadas.Should().NotContain("chave");
    }

    [Fact(DisplayName = "Conta inexistente é indistinguível de conta de outro titular")]
    public async Task ContaInexistente_EhIndistinguivelDeContaAlheia()
    {
        var resultado = await new RegistrarCreditoHandler(banco, banco, banco, relogio)
            .HandleAsync(new RegistrarCreditoCommand(Titular, ContaId.Novo(), Valor(1m), Descricao(), null, Chave()), Ct);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ContaNaoEncontrada);
    }

    [Fact(DisplayName = "Lançamento repetido com a mesma chave devolve o original sem criar outro")]
    public async Task Repeticao_DevolveOLancamentoOriginalSemCriarOutro()
    {
        var conta = await AbrirAsync();
        var credito = new RegistrarCreditoHandler(banco, banco, banco, relogio);

        var primeiro = await credito.HandleAsync(Comando(conta, Titular, 10m, "k1"), Ct);
        var segundo = await credito.HandleAsync(Comando(conta, Titular, 10m, "k1"), Ct);

        segundo.Valor.Should().Be(primeiro.Valor);
        banco.Lancamentos.Should().ContainSingle();
        conta.SaldoDisponivel.Should().Be(10m);
    }

    [Fact(DisplayName = "Mesma chave de idempotência com outro conteúdo é conflitante")]
    public async Task MesmaChaveComOutroConteudo_EhConflitante()
    {
        var conta = await AbrirAsync();
        await new RegistrarCreditoHandler(banco, banco, banco, relogio).HandleAsync(Comando(conta, Titular, 100m, "k0"), Ct);
        await Debitar(conta, Titular, 30m, "k1");

        var resultado = await Debitar(conta, Titular, 50m, "k1");

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaConflitante);
        conta.SaldoDisponivel.Should().Be(70m);
    }

    [Fact(DisplayName = "Mesma chave de idempotência para outro tipo de operação é conflitante")]
    public async Task MesmaChaveParaOutroTipo_EhConflitante()
    {
        var conta = await AbrirAsync();
        await new RegistrarCreditoHandler(banco, banco, banco, relogio).HandleAsync(Comando(conta, Titular, 100m, "k1"), Ct);

        var resultado = await Debitar(conta, Titular, 100m, "k1");

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaConflitante);
    }

    [Fact(DisplayName = "Data de competência omitida na repetição não entra na comparação com o original")]
    public async Task CompetenciaOmitidaNaRepeticao_NaoEntraNaComparacao()
    {
        var conta = await AbrirAsync();
        var credito = new RegistrarCreditoHandler(banco, banco, banco, relogio);
        var competencia = Agora.AddHours(-1);

        var primeiro = await credito.HandleAsync(Comando(conta, Titular, 10m, "k1") with { DataCompetencia = competencia }, Ct);
        var repeticao = await credito.HandleAsync(Comando(conta, Titular, 10m, "k1"), Ct);
        var outraCompetencia = await credito.HandleAsync(Comando(conta, Titular, 10m, "k1") with { DataCompetencia = Agora.AddHours(-2) }, Ct);

        repeticao.Valor.Should().Be(primeiro.Valor);
        outraCompetencia.Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaConflitante);
    }

    [Fact(DisplayName = "Operação recusada não é gravada, e a repetição dela é avaliada de novo")]
    public async Task OperacaoRecusada_NaoEhGravadaEARepeticaoEhReavaliada()
    {
        var conta = await AbrirAsync();

        var recusado = await Debitar(conta, Titular, 30m, "k1");
        await new RegistrarCreditoHandler(banco, banco, banco, relogio).HandleAsync(Comando(conta, Titular, 50m, "k2"), Ct);
        var aceito = await Debitar(conta, Titular, 30m, "k1");

        recusado.Falha?.Codigo.Should().Be(CodigosDeFalha.SaldoInsuficiente);
        aceito.Sucesso.Should().BeTrue();
        banco.Lancamentos.Count.Should().Be(2);
    }

    [Fact(DisplayName = "Estorno repetido com a mesma chave devolve o mesmo estorno")]
    public async Task Estorno_RepetidoComAMesmaChave_DevolveOMesmoEstorno()
    {
        var conta = await AbrirAsync();
        var credito = await new RegistrarCreditoHandler(banco, banco, banco, relogio).HandleAsync(Comando(conta, Titular, 10m, "k1"), Ct);
        var estornar = new EstornarLancamentoHandler(banco, banco, banco, relogio);
        var comando = new EstornarLancamentoCommand(Titular, conta.Id, new LancamentoId(credito.Valor.LancamentoId), null, Chave("e1"));

        var primeiro = await estornar.HandleAsync(comando, Ct);
        var repeticao = await estornar.HandleAsync(comando, Ct);
        var outraChave = await estornar.HandleAsync(comando with { Chave = Chave("e2") }, Ct);

        primeiro.Valor.Tipo.Should().Be(nameof(TipoLancamento.Estorno));
        repeticao.Valor.Should().Be(primeiro.Valor);
        outraChave.Falha?.Codigo.Should().Be(CodigosDeFalha.LancamentoJaEstornado);
        conta.SaldoDisponivel.Should().Be(0m);
    }

    [Fact(DisplayName = "Estorno de lançamento inexistente responde que o lançamento não foi encontrado")]
    public async Task Estorno_DeLancamentoInexistente_NaoEhEncontrado()
    {
        var conta = await AbrirAsync();

        var resultado = await new EstornarLancamentoHandler(banco, banco, banco, relogio)
            .HandleAsync(new EstornarLancamentoCommand(Titular, conta.Id, LancamentoId.Novo(), null, Chave()), Ct);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.LancamentoNaoEncontrado);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RegistrarCreditoCommand Comando(Conta conta, TitularId titular, decimal valor, string chave) =>
        new(titular, conta.Id, Valor(valor), Descricao(), null, Chave(chave));

    private static RegistrarDebitoCommand Debito(Conta conta, TitularId titular, decimal valor, string chave) =>
        new(titular, conta.Id, Valor(valor), Descricao(), null, Chave(chave));

    private async Task<Conta> AbrirAsync()
    {
        var aberta = (await new AbrirContaHandler(banco, banco, relogio).HandleAsync(new AbrirContaCommand(Titular, Chave()), Ct)).Valor;
        return banco.Contas.Single(c => c.Id.Valor == aberta.ContaId);
    }

    private Task<Resultado<LancamentoResponse>> Debitar(Conta conta, TitularId titular, decimal valor, string chave) =>
        valor == 0m
            ? new RegistrarCreditoHandler(banco, banco, banco, relogio).HandleAsync(Comando(conta, titular, 1m, chave), Ct)
            : new RegistrarDebitoHandler(banco, banco, banco, relogio).HandleAsync(Debito(conta, titular, valor, chave), Ct);
}
