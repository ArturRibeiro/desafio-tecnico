namespace Posicao.Unit.Tests.Dominio;

/// <summary>Tarefa 2.1: Value Objects próprios de Posição.</summary>
public sealed class ValueObjectsTestes
{
    [Theory(DisplayName = "Valor fora das regras é recusado")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.005")]
    [InlineData("1000000000000.00")]
    public void Valor_ForaDasRegras_ERecusado(string quantia)
    {
        var resultado = Valor.Criar(decimal.Parse(quantia, System.Globalization.CultureInfo.InvariantCulture));

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ValorInvalido);
    }

    [Theory(DisplayName = "Valor dentro das regras fica com duas casas decimais")]
    [InlineData("0.01", "0.01")]
    [InlineData("30", "30.00")]
    [InlineData("10.500", "10.50")]
    [InlineData("999999999999.99", "999999999999.99")]
    public void Valor_DentroDasRegras_FicaComDuasCasas(string quantia, string esperado)
    {
        var resultado = Valor.Criar(decimal.Parse(quantia, System.Globalization.CultureInfo.InvariantCulture));

        resultado.Valor.Quantia.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be(esperado);
    }

    [Fact(DisplayName = "Titular vazio ou longo demais é recusado")]
    public void TitularId_VazioOuLongo_ERecusado()
    {
        TitularId.Criar(" ").Falha?.Codigo.Should().Be(CodigosDeFalha.TitularInvalido);
        TitularId.Criar(new string('a', 201)).Falha?.Codigo.Should().Be(CodigosDeFalha.TitularInvalido);
        TitularId.Criar(new string('a', 200)).Sucesso.Should().BeTrue();
    }

    [Fact(DisplayName = "Data de fechamento usa o dia em UTC")]
    public void DataFechamento_UsaODiaEmUtc()
    {
        var instante = new DateTimeOffset(2026, 10, 27, 22, 30, 0, TimeSpan.FromHours(-3));

        var data = DataFechamento.De(instante);

        data.Valor.Should().Be(new DateOnly(2026, 10, 28));
        data.Inicio.Should().Be(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Movimento com sequência zero é recusado")]
    public void Movimento_SequenciaZero_ERecusado()
    {
        var resultado = Movimento.Criar(DominioFactory.NovaConta(), 0, new LancamentoId(Guid.NewGuid()), TipoMovimento.Credito, Valor.Criar(1m).Valor, DominioFactory.Dia1, DominioFactory.Dia1, null);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.MovimentoInvalido);
    }

    [Fact(DisplayName = "Movimento de estorno sem original ou de crédito com original é recusado")]
    public void Movimento_EstornoSemOriginalOuCreditoComOriginal_ERecusado()
    {
        var conta = DominioFactory.NovaConta();
        var valor = Valor.Criar(1m).Valor;
        var lancamento = new LancamentoId(Guid.NewGuid());

        Movimento.Criar(conta, 1, lancamento, TipoMovimento.Estorno, valor, DominioFactory.Dia1, DominioFactory.Dia1, null).Sucesso.Should().BeFalse();
        Movimento.Criar(conta, 1, lancamento, TipoMovimento.Credito, valor, DominioFactory.Dia1, DominioFactory.Dia1, lancamento).Sucesso.Should().BeFalse();
    }
}
