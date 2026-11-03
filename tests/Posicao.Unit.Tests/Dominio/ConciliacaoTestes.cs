namespace Posicao.Unit.Tests.Dominio;

/// <summary>Spec projecao-da-posicao: conciliação de cada movimento com o saldo informado por Lançamentos.</summary>
public sealed class ConciliacaoTestes
{
    private static readonly DateTimeOffset Dia1 = DominioFactory.Dia1;

    [Fact(DisplayName = "Saldo calculado igual ao informado não gera divergência")]
    public void SaldoIgual_NaoGeraDivergencia()
    {
        var conta = DominioFactory.NovaConta();
        var pendentes = new[]
        {
            DominioFactory.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m),
            DominioFactory.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 70.0m),
        };

        var resultado = ProjecaoDaConta.Aplicar(DominioFactory.Titularidade(conta), null, pendentes, []);

        resultado.Aplicados.Should().HaveCount(2);
        resultado.Divergencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Saldo divergente é apontado sem bloquear a aplicação nem mudar o saldo calculado")]
    public void SaldoDivergente_EApontado_SemBloquearNemMudarOSaldo()
    {
        var conta = DominioFactory.NovaConta();
        var debito = DominioFactory.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 50m);
        var pendentes = new[]
        {
            DominioFactory.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m),
            debito,
            DominioFactory.Credito(conta, 3, 10m, Dia1.AddHours(3)),
        };

        var resultado = ProjecaoDaConta.Aplicar(DominioFactory.Titularidade(conta), null, pendentes, []);

        resultado.Interrupcao.Should().BeNull();
        resultado.Aplicados.Select(m => m.Sequencia).Should().Equal([1L, 2L, 3L]);
        resultado.NovosFechamentos.Single().SaldoFechamento.Should().Be(80.00m);
        resultado.Divergencias.Should().Equal(new Divergencia(conta, 2, debito.LancamentoId));
    }

    [Fact(DisplayName = "Movimento sem saldo informado é aplicado sem conciliação")]
    public void SemSaldoInformado_NaoEConciliado()
    {
        var conta = DominioFactory.NovaConta();

        var resultado = ProjecaoDaConta.Aplicar(DominioFactory.Titularidade(conta), null, [DominioFactory.Credito(conta, 1, 10m, Dia1.AddHours(1))], []);

        resultado.Aplicados.Should().ContainSingle();
        resultado.Divergencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Movimento divergente que estava pendente é apontado quando a lacuna é preenchida")]
    public void MovimentoPendente_EConciliadoQuandoALacunaEPreenchida()
    {
        var conta = DominioFactory.NovaConta();
        var titularidade = DominioFactory.Titularidade(conta);
        var fechamento = ProjecaoDaConta.Aplicar(titularidade, null, [DominioFactory.Credito(conta, 1, 10m, Dia1.AddHours(1), saldoInformado: 10m)], [])
            .NovosFechamentos.Single();
        var terceira = DominioFactory.Credito(conta, 3, 30m, Dia1.AddHours(3), saldoInformado: 99m);

        ProjecaoDaConta.Aplicar(titularidade, fechamento, [terceira], []).Divergencias.Should().BeEmpty();
        var comASegunda = ProjecaoDaConta.Aplicar(
            titularidade, fechamento, [terceira, DominioFactory.Credito(conta, 2, 20m, Dia1.AddHours(2), saldoInformado: 30m)], []);

        comASegunda.Divergencias.Should().Equal(new Divergencia(conta, 3, terceira.LancamentoId));
    }

    [Fact(DisplayName = "Reconstrução aponta de novo a divergência dos movimentos recebidos")]
    public void Reconstrucao_ApontaADivergenciaDeNovo()
    {
        var conta = DominioFactory.NovaConta();
        var titularidade = DominioFactory.Titularidade(conta);
        var debito = DominioFactory.Debito(conta, 2, 30m, Dia1.AddHours(2), saldoInformado: 50m);
        var todos = new[] { DominioFactory.Credito(conta, 1, 100m, Dia1.AddHours(1), saldoInformado: 100m), debito };
        ProjecaoDaConta.Aplicar(titularidade, null, todos, []).Divergencias.Should().ContainSingle();

        var reconstrucao = ProjecaoDaConta.Reconstruir(titularidade, todos);

        reconstrucao.Divergencias.Should().Equal(new Divergencia(conta, 2, debito.LancamentoId));
    }
}
