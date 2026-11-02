namespace Lancamentos.Unit.Tests.Dominio;

public sealed class ContaTestes
{
    [Fact(DisplayName = "Conta aberta nasce ativa, com saldo e limite zero, e publica ContaAberta")]
    public void Abrir_NasceAtivaComSaldoELimiteZeroEPublicaContaAberta()
    {
        var conta = Conta.Abrir(ContaId.Novo(), Titular, Chave("abertura-1"), Agora);

        conta.Situacao.Should().Be(SituacaoConta.Ativa);
        conta.SaldoDisponivel.Should().Be(0m);
        conta.Limite.Should().Be(0m);
        conta.UltimaSequencia.Should().Be(0);
        conta.PertenceA(Titular).Should().BeTrue();
        conta.PertenceA(TitularId.Criar("cliente-b").Valor).Should().BeFalse();
        var evento = conta.Eventos.Should().ContainSingle().Subject.Should().BeOfType<ContaAberta>().Subject;
        evento.Conta.Should().BeSameAs(conta);
    }

    [Fact(DisplayName = "Crédito aumenta o saldo e recebe a sequência 1")]
    public void Credito_AumentaSaldoEUsaSequencia1()
    {
        var conta = ContaComSaldo(0);

        var lancamento = conta.RegistrarCredito(Valor(100m), Descricao(), null, Chave(), Agora).Valor;

        lancamento.Tipo.Should().Be(TipoLancamento.Credito);
        lancamento.Sequencia.Should().Be(1);
        lancamento.SaldoDisponivelApos.Should().Be(100m);
        conta.SaldoDisponivel.Should().Be(100m);
        conta.Eventos.Should().ContainSingle().Subject.Should().BeOfType<LancamentoRegistrado>();
    }

    [Fact(DisplayName = "Débito dentro do saldo reduz o saldo")]
    public void Debito_DentroDoSaldo_ReduzSaldo()
    {
        var conta = ContaComSaldo(100m);

        var lancamento = conta.RegistrarDebito(Valor(30m), Descricao(), null, Chave(), Agora).Valor;

        lancamento.Tipo.Should().Be(TipoLancamento.Debito);
        conta.SaldoDisponivel.Should().Be(70m);
        lancamento.SaldoDisponivelApos.Should().Be(70m);
    }

    [Fact(DisplayName = "Débito de todo o saldo deixa o saldo zerado")]
    public void Debito_DeTodoOSaldo_DeixaSaldoZero()
    {
        var conta = ContaComSaldo(100m);

        conta.RegistrarDebito(Valor(100m), Descricao(), null, Chave(), Agora).Sucesso.Should().BeTrue();
        conta.SaldoDisponivel.Should().Be(0m);
    }

    [Fact(DisplayName = "Débito acima do saldo é recusado sem alterar a conta")]
    public void Debito_AcimaDoSaldo_EhRecusadoSemAlterarNada()
    {
        var conta = ContaComSaldo(100m);
        var versao = conta.Versao;

        var resultado = conta.RegistrarDebito(Valor(100.01m), Descricao(), null, Chave(), Agora);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.SaldoInsuficiente);
        conta.SaldoDisponivel.Should().Be(100m);
        conta.UltimaSequencia.Should().Be(1);
        conta.Versao.Should().Be(versao);
        conta.Eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Operação recusada não consome sequência")]
    public void OperacaoRecusada_NaoConsomeSequencia()
    {
        var conta = ContaComSaldo(0);

        var primeiro = conta.RegistrarCredito(Valor(10m), Descricao(), null, Chave(), Agora).Valor;
        conta.RegistrarDebito(Valor(50m), Descricao(), null, Chave(), Agora).Sucesso.Should().BeFalse();
        var segundo = conta.RegistrarCredito(Valor(10m), Descricao(), null, Chave(), Agora).Valor;

        primeiro.Sequencia.Should().Be(1);
        segundo.Sequencia.Should().Be(2);
    }

    [Theory(DisplayName = "Conta que não está ativa recusa lançamentos")]
    [InlineData(SituacaoConta.Bloqueada)]
    [InlineData(SituacaoConta.Encerrada)]
    public void ContaNaoAtiva_RecusaLancamentos(SituacaoConta situacao)
    {
        var conta = ContaComSaldo(100m);
        typeof(Conta).GetProperty(nameof(Conta.Situacao))!.SetValue(conta, situacao, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);

        conta.RegistrarCredito(Valor(1m), Descricao(), null, Chave(), Agora).Falha?.Codigo.Should().Be(CodigosDeFalha.ContaInativa);
        conta.RegistrarDebito(Valor(1m), Descricao(), null, Chave(), Agora).Falha?.Codigo.Should().Be(CodigosDeFalha.ContaInativa);
    }

    [Fact(DisplayName = "Data de competência omitida usa o instante do registro")]
    public void Competencia_Omitida_UsaOInstanteDoRegistro()
    {
        var lancamento = ContaComSaldo(0).RegistrarCredito(Valor(1m), Descricao(), null, Chave(), Agora).Valor;

        lancamento.DataCompetencia.Should().Be(Agora);
        lancamento.DataRegistro.Should().Be(Agora);
    }

    [Fact(DisplayName = "Data de competência no início do dia (UTC) é aceita")]
    public void Competencia_NoInicioDoDiaUtc_EhAceita()
    {
        var inicioDoDia = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);

        ContaComSaldo(0).RegistrarCredito(Valor(1m), Descricao(), inicioDoDia, Chave(), Agora).Sucesso.Should().BeTrue();
    }

    [Theory(DisplayName = "Data de competência no futuro ou em dia anterior é recusada")]
    [InlineData(1)]
    [InlineData(-16)]
    public void Competencia_NoFuturoOuEmDiaAnterior_EhRecusada(int horasEmRelacaoAAgora)
    {
        var competencia = Agora.AddHours(horasEmRelacaoAAgora);

        var resultado = ContaComSaldo(0).RegistrarCredito(Valor(1m), Descricao(), competencia, Chave(), Agora);

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.DataCompetenciaInvalida);
    }

    [Fact(DisplayName = "Data de competência com outro fuso é comparada em UTC")]
    public void Competencia_ComOutroFuso_EhComparadaEmUtc()
    {
        // 26/10 22:00 em -03:00 é 27/10 01:00 UTC: está no dia corrente.
        var competencia = new DateTimeOffset(2026, 10, 26, 22, 0, 0, TimeSpan.FromHours(-3));

        ContaComSaldo(0).RegistrarCredito(Valor(1m), Descricao(), competencia, Chave(), Agora).Sucesso.Should().BeTrue();
    }

    [Fact(DisplayName = "Lançamento registrado não tem como ser alterado")]
    public void Lancamento_NaoTemComoSerAlterado()
    {
        var setters = typeof(Lancamento).GetProperties().Where(p => p.SetMethod?.IsPublic == true);

        setters.Should().BeEmpty();
    }
}
