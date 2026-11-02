namespace Posicao.Unit.Tests.Dominio;

/// <summary>Tarefas 2.2 e 2.3: spec projecao-da-posicao (ordem, lacuna, fechamento diário e estorno).</summary>
public sealed class ProjecaoDaContaTestes
{
    private static readonly DateTimeOffset Dia1 = Fabrica.Dia1;
    private static readonly DateTimeOffset Dia3 = Fabrica.Dia1.AddDays(2);

    [Fact(DisplayName = "Projeção aplica os movimentos em ordem de sequência, mesmo recebidos fora de ordem")]
    public void AplicaEmOrdemDeSequencia_MesmoRecebidosForaDeOrdem()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var pendentes = new[]
        {
            Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(15)),
            Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(10)),
        };

        var resultado = ProjecaoDaConta.Aplicar(titularidade, null, pendentes, []);

        resultado.Interrupcao.Should().BeNull();
        resultado.Aplicados.Select(m => m.Sequencia).Should().Equal([1L, 2L]);
        var fechamento = resultado.NovosFechamentos.Should().ContainSingle().Subject;
        fechamento.SaldoFechamento.Should().Be(70.00m);
        titularidade.UltimaSequenciaAplicada.Should().Be(2);
        titularidade.AtualizadoAte.Should().Be(pendentes[0].DataRegistro);
    }

    [Fact(DisplayName = "Projeção para na lacuna de sequência e aplica os seguintes quando ela é preenchida")]
    public void ParaNaLacuna_EAplicaOsSeguintesQuandoElaEPreenchida()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var primeiro = ProjecaoDaConta.Aplicar(titularidade, null, [Fabrica.Credito(conta, 1, 10m, Dia1.AddHours(1))], []);
        var fechamento = primeiro.NovosFechamentos.Should().ContainSingle().Subject;
        var terceira = Fabrica.Credito(conta, 3, 30m, Dia1.AddHours(3));

        var semASegunda = ProjecaoDaConta.Aplicar(titularidade, fechamento, [terceira], []);

        semASegunda.Aplicados.Should().BeEmpty();
        semASegunda.Interrupcao.Should().BeNull();
        titularidade.UltimaSequenciaAplicada.Should().Be(1);
        fechamento.SaldoFechamento.Should().Be(10.00m);
        terceira.Efeito.Should().BeNull();

        var comASegunda = ProjecaoDaConta.Aplicar(titularidade, fechamento, [terceira, Fabrica.Credito(conta, 2, 20m, Dia1.AddHours(2))], []);

        comASegunda.Aplicados.Select(m => m.Sequencia).Should().Equal([2L, 3L]);
        fechamento.SaldoFechamento.Should().Be(60.00m);
        titularidade.UltimaSequenciaAplicada.Should().Be(3);
    }

    [Fact(DisplayName = "Movimento com sequência já aplicada é ignorado")]
    public void SequenciaJaAplicada_EIgnorada()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var credito = Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1));
        var fechamento = ProjecaoDaConta.Aplicar(titularidade, null, [credito], []).NovosFechamentos.Single();

        var repetido = ProjecaoDaConta.Aplicar(titularidade, fechamento, [Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1))], []);

        repetido.Aplicados.Should().BeEmpty();
        fechamento.SaldoFechamento.Should().Be(100.00m);
        fechamento.Aplicar(credito);
        fechamento.SaldoFechamento.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Troca de dia abre novo fechamento com abertura igual ao fechamento anterior")]
    public void TrocaDeDia_AbreNovoFechamentoComAberturaIgualAoFechamentoAnterior()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);

        var resultado = ProjecaoDaConta.Aplicar(
            titularidade,
            null,
            [Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(10)), Fabrica.Debito(conta, 2, 30m, Dia3.AddHours(9))],
            []);

        resultado.NovosFechamentos.Should().SatisfyRespectively(d1 =>
            {
                d1.Data.Should().Be(DataFechamento.De(Dia1));
                (d1.SaldoAbertura, d1.TotalCreditos, d1.TotalDebitos, d1.SaldoFechamento).Should().Be((0.00m, 100.00m, 0.00m, 100.00m));
            }, d3 =>
            {
                d3.Data.Should().Be(DataFechamento.De(Dia3));
                (d3.SaldoAbertura, d3.TotalCreditos, d3.TotalDebitos, d3.SaldoFechamento).Should().Be((100.00m, 0.00m, 30.00m, 70.00m));
            });
    }

    [Fact(DisplayName = "Estorno de débito conta como crédito no fechamento")]
    public void EstornoDeDebito_ContaComoCredito()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var credito = Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1));
        var debito = Fabrica.Debito(conta, 2, 30m, Dia1.AddHours(2));
        var fechamento = ProjecaoDaConta.Aplicar(titularidade, null, [credito, debito], []).NovosFechamentos.Single();

        var resultado = ProjecaoDaConta.Aplicar(titularidade, fechamento, [Fabrica.Estorno(conta, 3, debito, Dia1.AddHours(3))], [debito]);

        resultado.Aplicados.Should().ContainSingle().Subject.Efeito.Should().Be(Efeito.Credito);
        fechamento.TotalCreditos.Should().Be(130.00m);
        fechamento.SaldoFechamento.Should().Be(100.00m);
    }

    [Fact(DisplayName = "Estorno de crédito no mesmo lote conta como débito no fechamento")]
    public void EstornoDeCredito_NoMesmoLote_ContaComoDebito()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var credito = Fabrica.Credito(conta, 1, 100m, Dia1.AddHours(1));

        var resultado = ProjecaoDaConta.Aplicar(titularidade, null, [Fabrica.Estorno(conta, 2, credito, Dia1.AddHours(2)), credito], []);

        (resultado.Aplicados[1].Efeito).Should().Be(Efeito.Debito);
        resultado.NovosFechamentos.Single().SaldoFechamento.Should().Be(0.00m);
    }

    [Fact(DisplayName = "Estorno sem o lançamento original interrompe a projeção")]
    public void EstornoSemOriginal_Interrompe()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var desconhecido = Fabrica.Credito(conta, 9, 10m, Dia1);

        var resultado = ProjecaoDaConta.Aplicar(titularidade, null, [Fabrica.Estorno(conta, 1, desconhecido, Dia1.AddHours(1))], []);

        resultado.Interrupcao?.Codigo.Should().Be(CodigosDeFalha.EstornoSemOriginal);
        titularidade.UltimaSequenciaAplicada.Should().Be(0);
    }

    [Fact(DisplayName = "Movimento com competência antes do último fechamento interrompe a projeção")]
    public void CompetenciaAntesDoUltimoFechamento_Interrompe()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var fechamento = ProjecaoDaConta.Aplicar(titularidade, null, [Fabrica.Credito(conta, 1, 10m, Dia3)], []).NovosFechamentos.Single();

        var resultado = ProjecaoDaConta.Aplicar(titularidade, fechamento, [Fabrica.Credito(conta, 2, 10m, Dia1)], []);

        resultado.Interrupcao?.Codigo.Should().Be(CodigosDeFalha.CompetenciaForaDeOrdem);
        titularidade.UltimaSequenciaAplicada.Should().Be(1);
        fechamento.SaldoFechamento.Should().Be(10.00m);
    }

    [Fact(DisplayName = "Reconstrução produz o mesmo resultado do processamento original")]
    public void Reconstruir_ProduzOMesmoResultadoDoProcessamentoOriginal()
    {
        var conta = Fabrica.NovaConta();
        var titularidade = Fabrica.Titularidade(conta);
        var debito = Fabrica.Debito(conta, 2, 30m, Dia3);
        Movimento[] todos = [Fabrica.Credito(conta, 1, 100m, Dia1), debito, Fabrica.Estorno(conta, 3, debito, Dia3.AddHours(1))];
        var original = ProjecaoDaConta.Aplicar(titularidade, null, todos, []).NovosFechamentos;

        var reconstruido = ProjecaoDaConta.Reconstruir(titularidade, todos).NovosFechamentos;

        reconstruido.Select(f => (f.Data, f.SaldoAbertura, f.TotalCreditos, f.TotalDebitos, f.SaldoFechamento)).Should().Equal(original.Select(f => (f.Data, f.SaldoAbertura, f.TotalCreditos, f.TotalDebitos, f.SaldoFechamento)));
        titularidade.UltimaSequenciaAplicada.Should().Be(3);
    }
}
