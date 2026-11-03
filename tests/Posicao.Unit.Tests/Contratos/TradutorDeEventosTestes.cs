namespace Posicao.Unit.Tests.Contratos;

/// <summary>Tarefa 5.1: camada anticorrupção entre a Published Language de Lançamentos e o modelo de Posição.</summary>
public sealed class TradutorDeEventosTestes
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 27, 14, 32, 10, TimeSpan.Zero);

    [Fact(DisplayName = "Evento de movimentação válido vira um movimento pendente")]
    public void Movimentacao_Valida_ViraMovimentoPendente()
    {
        var estornado = Guid.NewGuid();

        var movimento = TradutorDeEventos.Traduzir(Movimentacao() with { Tipo = "Estorno", LancamentoEstornadoId = estornado });

        movimento.Tipo.Should().Be(TipoMovimento.Estorno);
        movimento.Valor.Quantia.Should().Be(150.00m);
        movimento.LancamentoEstornadoId?.Valor.Should().Be(estornado);
        movimento.Efeito.Should().BeNull();
    }

    [Fact(DisplayName = "Saldo informado por Lançamentos chega ao movimento, e a ausência dele também")]
    public void Movimentacao_LevaOSaldoInformado_QuandoHouver()
    {
        TradutorDeEventos.Traduzir(Movimentacao() with { SaldoDisponivelApos = 70.00m }).SaldoInformadoApos.Should().Be(70.00m);
        TradutorDeEventos.Traduzir(Movimentacao()).SaldoInformadoApos.Should().BeNull();
    }

    [Theory(DisplayName = "Evento de movimentação com tipo desconhecido é inválido")]
    [InlineData("Transferencia")]
    [InlineData("debito")]
    [InlineData("1")]
    public void Movimentacao_ComTipoDesconhecido_EInvalida(string tipo) =>
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Movimentacao() with { Tipo = tipo })).Should().ThrowExactly<EventoInvalidoException>();

    [Theory(DisplayName = "Evento de movimentação com valor inválido é inválido")]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("10.001")]
    public void Movimentacao_ComValorInvalido_EInvalida(string valor) =>
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Movimentacao() with { Valor = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture) })).Should().ThrowExactly<EventoInvalidoException>();

    [Fact(DisplayName = "Evento de movimentação em outra moeda que não BRL é inválido")]
    public void Movimentacao_EmOutraMoeda_EInvalida() =>
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Movimentacao() with { Moeda = "USD" })).Should().ThrowExactly<EventoInvalidoException>();

    [Fact(DisplayName = "Evento de movimentação com sequência zero é inválido")]
    public void Movimentacao_ComSequenciaZero_EInvalida() =>
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Movimentacao() with { Sequencia = 0 })).Should().ThrowExactly<EventoInvalidoException>();

    [Fact(DisplayName = "Evento de débito com lançamento estornado informado é inválido")]
    public void Movimentacao_DebitoComLancamentoEstornado_EInvalida() =>
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Movimentacao() with { LancamentoEstornadoId = Guid.NewGuid() })).Should().ThrowExactly<EventoInvalidoException>();

    [Fact(DisplayName = "Evento de abertura de conta válido vira o comando de abertura")]
    public void Abertura_Valida_ViraComando()
    {
        var conta = Guid.NewGuid();

        var comando = TradutorDeEventos.Traduzir(Abertura() with { ContaId = conta });

        comando.ContaId.Valor.Should().Be(conta);
        comando.Titular.Valor.Should().Be("titular-1");
    }

    [Fact(DisplayName = "Evento de abertura sem titular ou com sequência diferente de zero é inválido")]
    public void Abertura_SemTitularOuComSequenciaDiferenteDeZero_EInvalida()
    {
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Abertura() with { TitularId = " " })).Should().ThrowExactly<EventoInvalidoException>();
        FluentActions.Invoking(() => TradutorDeEventos.Traduzir(Abertura() with { Sequencia = 1 })).Should().ThrowExactly<EventoInvalidoException>();
    }

    private static MovimentacaoRegistradaV1 Movimentacao() => new()
    {
        EventoId = Guid.NewGuid(),
        ContaId = Guid.NewGuid(),
        LancamentoId = Guid.NewGuid(),
        Sequencia = 1,
        Tipo = "Debito",
        Valor = 150.00m,
        Moeda = "BRL",
        DataCompetencia = Instante,
        DataRegistro = Instante,
        CorrelacaoId = "correlacao",
    };

    private static ContaAbertaV1 Abertura() => new()
    {
        EventoId = Guid.NewGuid(),
        ContaId = Guid.NewGuid(),
        TitularId = "titular-1",
        Moeda = "BRL",
        DataAbertura = Instante,
        CorrelacaoId = "correlacao",
    };
}
