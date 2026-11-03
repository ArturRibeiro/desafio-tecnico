namespace Posicao.Unit.Tests.UseCases;

/// <summary>Tarefas 3.1 a 3.3: casos de uso de projeção, consultas e reconstrução, com portas em memória.</summary>
public sealed class CasosDeUsoTestes
{
    private static readonly DateTimeOffset Dia1 = Fabrica.Dia1;
    private static readonly DateTimeOffset Dia3 = Fabrica.Dia1.AddDays(2);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Memoria memoria = new();
    private readonly CacheFalso cache = new();
    private readonly DivergenciasRegistradas divergencias = new();
    private readonly ContaId conta = Fabrica.NovaConta();

    private AplicacaoDePendentes Pendentes => new(memoria, memoria, divergencias);

    [Fact(DisplayName = "Movimento recebido antes da abertura fica pendente e é aplicado na abertura")]
    public async Task Movimento_AntesDaAbertura_FicaPendenteEEAplicadoNaAbertura()
    {
        var resumo = await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(9)));

        resumo.Aplicados.Should().Be(0);
        memoria.Fechamentos.Should().BeEmpty();

        var abertura = await AbrirAsync();

        abertura.Aplicados.Should().Be(1);
        memoria.Fechamentos.Should().ContainSingle().Subject.SaldoFechamento.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Movimento repetido é reconhecido como já recebido e não muda nada")]
    public async Task Movimento_Repetido_EJaRecebidoENaoMudaNada()
    {
        await AbrirAsync();
        await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(9)));

        var repetido = await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(9)));

        repetido.JaRecebido.Should().BeTrue();
        memoria.Movimentos.Should().ContainSingle();
        memoria.Fechamentos.Should().ContainSingle().Subject.SaldoFechamento.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Abertura de conta repetida é reconhecida como já recebida")]
    public async Task Abertura_Repetida_EJaRecebida()
    {
        await AbrirAsync();

        (await AbrirAsync()).JaRecebido.Should().BeTrue();
        memoria.Titularidades.Should().ContainSingle();
    }

    [Fact(DisplayName = "Movimentos fora de ordem são aplicados quando a lacuna de sequência fecha")]
    public async Task Movimentos_ForaDeOrdem_SaoAplicadosQuandoALacunaFecha()
    {
        await AbrirAsync();
        await MovimentarAsync(Fabrica.Credito(conta, 2, 20m, Dia1.AddHours(10)));
        memoria.Fechamentos.Should().BeEmpty();

        var resumo = await MovimentarAsync(Fabrica.Credito(conta, 1, 10m, Dia1.AddHours(9)));

        resumo.Aplicados.Should().Be(2);
        memoria.Fechamentos.Should().ContainSingle().Subject.SaldoFechamento.Should().Be(30.00m);
    }

    [Fact(DisplayName = "Estorno encontra o lançamento original já aplicado")]
    public async Task Estorno_ResolveOOriginalJaAplicado()
    {
        await AbrirAsync();
        var debito = Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(10));
        await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(9)));
        await MovimentarAsync(debito);

        await MovimentarAsync(Fabrica.Estorno(conta, 3, debito, Dia1.AddHours(11)));

        memoria.Fechamentos.Should().ContainSingle().Subject.SaldoFechamento.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Posição em um instante do dia considera só os movimentos até esse instante")]
    public async Task Posicao_DentroDoDia_ConsideraSoOsMovimentosAteOInstante()
    {
        await CenarioDeDoisDiasAsync();

        var posicao = await PosicaoAsync(Dia3.AddHours(8));

        posicao.Valor.Saldo.Should().Be(100.00m);
        (await PosicaoAsync(Dia3.AddHours(10))).Valor.Saldo.Should().Be(70.00m);
        (await PosicaoAsync(Dia1.AddHours(12))).Valor.Saldo.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Posição sem instante informado usa o instante atual")]
    public async Task Posicao_SemInstante_UsaOAgora()
    {
        await CenarioDeDoisDiasAsync();

        var posicao = await PosicaoAsync(null);

        posicao.Valor.Saldo.Should().Be(70.00m);
        posicao.Valor.Instante.Should().Be(Dia3.AddDays(1));
    }

    [Fact(DisplayName = "Posição antes do primeiro movimento é zero")]
    public async Task Posicao_AntesDoPrimeiroMovimento_EZero()
    {
        await CenarioDeDoisDiasAsync();

        (await PosicaoAsync(Dia1.AddHours(1))).Valor.Saldo.Should().Be(0.00m);
        (await PosicaoAsync(Dia1.AddDays(-10))).Valor.Saldo.Should().Be(0.00m);
    }

    [Fact(DisplayName = "Posição ignora movimentos pendentes e informa a última sequência aplicada")]
    public async Task Posicao_IgnoraPendentesEInformaAUltimaSequenciaAplicada()
    {
        await AbrirAsync();
        var segunda = Fabrica.Credito(conta, 2, 20m, Dia1.AddHours(10));
        await MovimentarAsync(Fabrica.Credito(conta, 1, 10m, Dia1.AddHours(9)));
        await MovimentarAsync(segunda);
        await MovimentarAsync(Fabrica.Credito(conta, 4, 40m, Dia1.AddHours(12)));

        var posicao = await PosicaoAsync(Dia1.AddHours(23));

        posicao.Valor.Saldo.Should().Be(30.00m);
        posicao.Valor.AtualizadoAte.Should().Be(new AtualizadoAteResponse(2, segunda.DataRegistro));
    }

    [Fact(DisplayName = "Posição de conta sem movimento informa a abertura")]
    public async Task Posicao_SemMovimento_InformaAAbertura()
    {
        var titularidade = (await AbrirAsync(), memoria.Titularidades.Single()).Item2;

        var posicao = await PosicaoAsync(Dia1.AddHours(23));

        posicao.Valor.AtualizadoAte.Should().Be(new AtualizadoAteResponse(0, titularidade.DataAbertura));
    }

    [Fact(DisplayName = "Posição em instante no futuro é inválida")]
    public async Task Posicao_InstanteNoFuturo_EInvalido()
    {
        await AbrirAsync();

        var posicao = await PosicaoAsync(Dia3.AddDays(2));

        posicao.Falha?.Codigo.Should().Be(CodigosDeFalha.InstanteInvalido);
    }

    [Fact(DisplayName = "Posição de conta de outro titular ou inexistente responde conta não encontrada")]
    public async Task Posicao_DeOutroTitularOuInexistente_EContaNaoEncontrada()
    {
        await AbrirAsync();

        var deOutro = await new ObterPosicaoEmHandler(memoria, memoria, memoria, cache, Relogio())
            .HandleAsync(new ObterPosicaoEmQuery(Fabrica.Titular("outro"), conta, null), Ct);
        var inexistente = await new ObterPosicaoEmHandler(memoria, memoria, memoria, cache, Relogio())
            .HandleAsync(new ObterPosicaoEmQuery(Fabrica.Titular(), Fabrica.NovaConta(), null), Ct);

        deOutro.Falha?.Codigo.Should().Be(CodigosDeFalha.ContaNaoEncontrada);
        inexistente.Falha.Should().Be(deOutro.Falha);
    }

    [Fact(DisplayName = "Cache guarda só aberturas de dia definitivas")]
    public async Task Cache_GuardaSoAberturasDefinitivas()
    {
        await CenarioDeDoisDiasAsync();

        await PosicaoAsync(Dia3.AddHours(10));
        await PosicaoAsync(Dia3.AddDays(1));

        (cache.Aberturas[(conta, DataFechamento.De(Dia3))]).Should().Be(100.00m);
        (cache.Aberturas.ContainsKey((conta, DataFechamento.De(Dia3.AddDays(1))))).Should().BeFalse();
    }

    [Fact(DisplayName = "Cache é usado quando já tem o valor")]
    public async Task Cache_EUsadoQuandoTemOValor()
    {
        await CenarioDeDoisDiasAsync();
        cache.Aberturas[(conta, DataFechamento.De(Dia3))] = 500m;

        (await PosicaoAsync(Dia3.AddHours(10))).Valor.Saldo.Should().Be(470.00m);
    }

    [Fact(DisplayName = "Cache fora do ar não muda o resultado da consulta")]
    public async Task Cache_ForaDoAr_NaoMudaOResultado()
    {
        await CenarioDeDoisDiasAsync();
        cache.ForaDoAr = true;

        (await PosicaoAsync(Dia3.AddHours(10))).Valor.Saldo.Should().Be(70.00m);
    }

    [Fact(DisplayName = "Fechamentos listam só os dias com movimento, em ordem")]
    public async Task Fechamentos_ListaSoDiasComMovimentoEmOrdem()
    {
        await CenarioDeDoisDiasAsync();

        var pagina = await FechamentosAsync(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 31));

        pagina.Valor.Itens.Select(f => DateOnly.Parse(f.Data, System.Globalization.CultureInfo.InvariantCulture)).Should().Equal([new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 29)]);
        pagina.Valor.TemProximaPagina.Should().BeFalse();
    }

    [Fact(DisplayName = "Fechamentos são paginados")]
    public async Task Fechamentos_Paginados()
    {
        await AbrirAsync();
        for (var dia = 0; dia < 3; dia++)
        {
            await MovimentarAsync(Fabrica.Credito(conta, dia + 1, 10m, Dia1.AddDays(dia)));
        }

        var primeira = await FechamentosAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), pagina: 1, tamanho: 2);
        var segunda = await FechamentosAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), pagina: 2, tamanho: 2);

        primeira.Valor.Itens.Count.Should().Be(2);
        primeira.Valor.TemProximaPagina.Should().BeTrue();
        DateOnly.Parse(segunda.Valor.Itens.Should().ContainSingle().Subject.Data, System.Globalization.CultureInfo.InvariantCulture).Should().Be(new DateOnly(2026, 10, 29));
        segunda.Valor.TemProximaPagina.Should().BeFalse();
    }

    [Theory(DisplayName = "Fechamentos com período ou página inválidos são recusados")]
    [InlineData("2026-10-31", "2026-10-27", 1, 30)]
    [InlineData("2026-01-01", "2027-02-05", 1, 30)]
    [InlineData("2026-10-01", "2026-10-31", 0, 30)]
    [InlineData("2026-10-01", "2026-10-31", 1, 0)]
    [InlineData("2026-10-01", "2026-10-31", 1, 101)]
    [InlineData(null, "2026-10-31", 1, 30)]
    public async Task Fechamentos_PeriodoOuPaginaInvalidos(string? de, string ate, int pagina, int tamanho)
    {
        await AbrirAsync();

        var resultado = await FechamentosAsync(de is null ? null : DateOnly.Parse(de, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(ate, System.Globalization.CultureInfo.InvariantCulture), pagina, tamanho);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.PeriodoInvalido);
    }

    [Fact(DisplayName = "Fechamentos com período de 366 dias são aceitos")]
    public async Task Fechamentos_PeriodoDe366Dias_EAceito()
    {
        await AbrirAsync();

        var resultado = await FechamentosAsync(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2));

        resultado.Sucesso.Should().BeTrue();
    }

    [Fact(DisplayName = "Reconstrução corrige fechamentos alterados e limpa o cache")]
    public async Task Reconstrucao_CorrigeFechamentosAlteradosELimpaOCache()
    {
        await CenarioDeDoisDiasAsync();
        var corretos = Instantaneo();
        memoria.Fechamentos.Clear();
        memoria.Fechamentos.Add(global::Posicao.Domain.Fechamentos.FechamentoDiario.Abrir(conta, DataFechamento.De(Dia1), 999m));
        cache.Aberturas[(conta, DataFechamento.De(Dia3))] = 999m;

        var contas = await Reconstruir().HandleAsync(new ReconstruirPosicaoCommand(), Ct);

        contas.Should().Be(1);
        Instantaneo().Should().Equal(corretos);
        cache.Aberturas.Should().BeEmpty();
        memoria.Titularidades.Single().UltimaSequenciaAplicada.Should().Be(2);
    }

    [Fact(DisplayName = "Reconstrução repetida não muda nada")]
    public async Task Reconstrucao_Repetida_NaoMudaNada()
    {
        await CenarioDeDoisDiasAsync();
        await Reconstruir().HandleAsync(new ReconstruirPosicaoCommand(), Ct);
        var depoisDaPrimeira = Instantaneo();

        await Reconstruir().HandleAsync(new ReconstruirPosicaoCommand(), Ct);

        Instantaneo().Should().Equal(depoisDaPrimeira);
    }

    [Fact(DisplayName = "Projeção registra a divergência de saldo e segue aplicando")]
    public async Task Projecao_RegistraADivergencia_ESegueAplicando()
    {
        await AbrirAsync();
        await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(10), saldoInformado: 100m));
        var debito = Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(11), saldoInformado: 50m);

        var resumo = await MovimentarAsync(debito);

        resumo.Aplicados.Should().Be(1);
        memoria.Fechamentos.Single().SaldoFechamento.Should().Be(70.00m);
        divergencias.Registradas.Should().Equal(new Divergencia(conta, 2, debito.LancamentoId));
    }

    [Fact(DisplayName = "Reconstrução registra de novo as divergências de saldo")]
    public async Task Reconstrucao_RegistraDeNovoAsDivergencias()
    {
        await AbrirAsync();
        var debito = Fabrica.Debito(conta, 1, 30m, Dia1.AddHours(11), saldoInformado: 0m);
        memoria.Movimentos.Add(debito);
        await MovimentarAsync(Fabrica.Credito(conta, 2, 10m, Dia1.AddHours(12)));
        divergencias.Registradas.Clear();

        await Reconstruir().HandleAsync(new ReconstruirPosicaoCommand(), Ct);

        divergencias.Registradas.Should().Equal(new Divergencia(conta, 1, debito.LancamentoId));
    }

    private ReconstruirPosicaoHandler Reconstruir() => new(memoria, memoria, memoria, memoria, cache, divergencias);

    private List<(DateOnly, decimal, decimal, decimal, decimal)> Instantaneo() =>
        memoria.Fechamentos.OrderBy(f => f.Data).Select(f => (f.Data.Valor, f.SaldoAbertura, f.TotalCreditos, f.TotalDebitos, f.SaldoFechamento)).ToList();

    /// <summary>Crédito de 100 às 10h do dia 27 e débito de 30 às 9h do dia 29.</summary>
    private async Task CenarioDeDoisDiasAsync()
    {
        await AbrirAsync();
        await MovimentarAsync(Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(10)));
        await MovimentarAsync(Fabrica.Debito(conta, 2, 30m, Dia3.AddHours(9)));
    }

    private Task<ResumoDaProjecao> AbrirAsync() =>
        new ProjetarAberturaHandler(memoria, memoria, Pendentes).HandleAsync(new ProjetarAberturaCommand(conta, Fabrica.Titular(), Dia1.AddHours(8)), Ct);

    private Task<ResumoDaProjecao> MovimentarAsync(Movimento movimento) =>
        new ProjetarMovimentacaoHandler(memoria, memoria, memoria, Pendentes).HandleAsync(new ProjetarMovimentacaoCommand(movimento), Ct);

    private Task<Resultado<PosicaoConsolidadaResponse>> PosicaoAsync(DateTimeOffset? instante) =>
        new ObterPosicaoEmHandler(memoria, memoria, memoria, cache, Relogio()).HandleAsync(new ObterPosicaoEmQuery(Fabrica.Titular(), conta, instante), Ct);

    private Task<Resultado<FechamentosDiariosResponse>> FechamentosAsync(DateOnly? de, DateOnly ate, int pagina = 1, int tamanho = 30) =>
        new ObterFechamentosDiariosHandler(memoria, memoria).HandleAsync(new ObterFechamentosDiariosQuery(Fabrica.Titular(), conta, de, ate, pagina, tamanho), Ct);

    private static RelogioFixo Relogio() => new(Dia3.AddDays(1));
}
