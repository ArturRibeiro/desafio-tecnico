namespace Lancamentos.Unit.Tests.Dominio;

public sealed class EstornoTestes
{
    [Fact(DisplayName = "Estorno de débito devolve o valor ao saldo")]
    public void EstornoDeDebito_DevolveOValor()
    {
        var conta = ContaComSaldo(100m);
        var debito = conta.RegistrarDebito(Valor(30m), Descricao(), null, Chave(), Agora).Valor;

        var estorno = conta.Estornar(debito, jaEstornado: false, null, Chave(), Agora).Valor;

        estorno.Tipo.Should().Be(TipoLancamento.Estorno);
        estorno.Valor.Quantia.Should().Be(30m);
        estorno.LancamentoEstornadoId.Should().Be(debito.Id);
        conta.SaldoDisponivel.Should().Be(100m);
        estorno.SaldoDisponivelApos.Should().Be(100m);
        estorno.Sequencia.Should().Be(debito.Sequencia + 1);
        estorno.Descricao.Valor.Should().Contain(debito.Id.ToString());
        (conta.Eventos[^1]).Should().BeOfType<LancamentoEstornado>();
    }

    [Fact(DisplayName = "Estorno de crédito retira o valor do saldo")]
    public void EstornoDeCredito_RetiraOValor()
    {
        var conta = ContaComSaldo(0);
        var credito = conta.RegistrarCredito(Valor(40m), Descricao(), null, Chave(), Agora).Valor;

        conta.Estornar(credito, false, Descricao("Crédito indevido"), Chave(), Agora).Sucesso.Should().BeTrue();
        conta.SaldoDisponivel.Should().Be(0m);
    }

    [Fact(DisplayName = "Estorno de crédito sem saldo suficiente é recusado")]
    public void EstornoDeCredito_SemSaldo_EhRecusado()
    {
        var conta = ContaComSaldo(0);
        var credito = conta.RegistrarCredito(Valor(100m), Descricao(), null, Chave(), Agora).Valor;
        conta.RegistrarDebito(Valor(60m), Descricao(), null, Chave(), Agora);

        var resultado = conta.Estornar(credito, false, null, Chave(), Agora);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.SaldoInsuficiente);
        conta.SaldoDisponivel.Should().Be(40m);
    }

    [Fact(DisplayName = "Estorno de lançamento já estornado é recusado")]
    public void LancamentoJaEstornado_EhRecusado()
    {
        var conta = ContaComSaldo(100m);
        var debito = conta.RegistrarDebito(Valor(30m), Descricao(), null, Chave(), Agora).Valor;

        var resultado = conta.Estornar(debito, jaEstornado: true, null, Chave(), Agora);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.LancamentoJaEstornado);
        conta.SaldoDisponivel.Should().Be(70m);
    }

    [Fact(DisplayName = "Estorno de um estorno é recusado")]
    public void EstornoDeEstorno_EhRecusado()
    {
        var conta = ContaComSaldo(100m);
        var debito = conta.RegistrarDebito(Valor(30m), Descricao(), null, Chave(), Agora).Valor;
        var estorno = conta.Estornar(debito, false, null, Chave(), Agora).Valor;

        conta.Estornar(estorno, false, null, Chave(), Agora).Falha?.Codigo.Should().Be(CodigosDeFalha.EstornoNaoEstornavel);
    }

    [Fact(DisplayName = "Estorno de lançamento de outra conta não encontra o lançamento")]
    public void LancamentoDeOutraConta_NaoEhEncontrado()
    {
        var outra = ContaComSaldo(0);
        var credito = outra.RegistrarCredito(Valor(10m), Descricao(), null, Chave(), Agora).Valor;

        var resultado = ContaComSaldo(100m).Estornar(credito, false, null, Chave(), Agora);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.LancamentoNaoEncontrado);
    }
}
