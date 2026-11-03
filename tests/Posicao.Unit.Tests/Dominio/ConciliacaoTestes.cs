namespace Posicao.Unit.Tests.Dominio;

/// <summary>Spec projecao-da-posicao: conciliação de cada movimento com o saldo informado por Lançamentos.</summary>
public sealed class ConciliacaoTestes
{
    private static readonly DateTimeOffset Dia1 = Fabrica.Dia1;

    [Fact(DisplayName = "Saldo calculado igual ao informado não gera divergência")]
    public void SaldoIgual_NaoGeraDivergencia()
    {
        var conta = Fabrica.NovaConta();
        var pendentes = new[]
        {
            Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m),
            Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 70.0m),
        };

        var resultado = ProjecaoDaConta.Aplicar(Fabrica.Titularidade(conta), null, pendentes, []);

        resultado.Aplicados.Should().HaveCount(2);
        resultado.Divergencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Saldo divergente é apontado sem bloquear a aplicação nem mudar o saldo calculado")]
    public void SaldoDivergente_EApontado_SemBloquearNemMudarOSaldo()
    {
        var conta = Fabrica.NovaConta();
        var debito = Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 50m);
        var pendentes = new[]
        {
            Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m),
            debito,
            Fabrica.Credito(conta, 3, 10m, Dia1.AddHours(3)),
        };

        var resultado = ProjecaoDaConta.Aplicar(Fabrica.Titularidade(conta), null, pendentes, []);

        resultado.Interrupcao.Should().BeNull();
        resultado.Aplicados.Select(m => m.Sequencia).Should().Equal([1L, 2L, 3L]);
        resultado.NovosFechamentos.Single().SaldoFechamento.Should().Be(80.00m);
        resultado.Divergencias.Should().Equal(new Divergencia(conta, 2, debito.LancamentoId));
    }

    [Fact(DisplayName = "Movimento sem saldo informado é aplicado sem conciliação")]
    public void SemSaldoInformado_NaoEConciliado()
    {
        var conta = Fabrica.NovaConta();

        var resultado = ProjecaoDaConta.Aplicar(Fabrica.Titularidade(conta), null, [Fabrica.Credito(conta, 1, 10m, Dia1.AddHours(1))], []);

        resultado.Aplicados.Should().ContainSingle();
        resultado.Divergencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Movimento divergente que estava pendente é apontado quando a lacuna é preenchida")]
    public void MovimentoPendente_EConciliadoQuandoALacunaEPreenchida()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var fechamento = ProjecaoDaConta.Aplicar(titularidade, null, [Fabrica.Credito(conta, 1, 10m, Dia1.AddHours(1), saldoInformado: 10m)], [])
            .NovosFechamentos.Single();
        var terceira = Fabrica.Credito(conta, 3, 30m, Dia1.AddHours(3), saldoInformado: 99m);

        ProjecaoDaConta.Aplicar(titularidade, fechamento, [terceira], []).Divergencias.Should().BeEmpty();
        var comASegunda = ProjecaoDaConta.Aplicar(
            titularidade, fechamento, [terceira, Fabrica.Credito(conta, 2, 20m, Dia1.AddHours(2), saldoInformado: 30m)], []);

        comASegunda.Divergencias.Should().Equal(new Divergencia(conta, 3, terceira.LancamentoId));
    }

    [Fact(DisplayName = "Reconstrução aponta de novo a divergência dos movimentos recebidos")]
    public void Reconstrucao_ApontaADivergenciaDeNovo()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var debito = Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 50m);
        var todos = new[] { Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m), debito };
        ProjecaoDaConta.Aplicar(titularidade, null, todos, []).Divergencias.Should().ContainSingle();

        var reconstrucao = ProjecaoDaConta.Reconstruir(titularidade, todos);

        reconstrucao.Divergencias.Should().Equal(new Divergencia(conta, 2, debito.LancamentoId));
    }
}
