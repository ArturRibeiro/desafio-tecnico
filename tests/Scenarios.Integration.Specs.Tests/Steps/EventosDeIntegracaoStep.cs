namespace Scenarios.Integration.Specs.Tests.Steps;

/// <summary>Spec eventos-de-integracao: o que Lançamentos publica, quando, e com que conteúdo.</summary>
[Binding]
public sealed class EventosDeIntegracaoStep(SpecScenarioContext contexto)
{
    private static readonly TimeSpan JanelaSemEvento = TimeSpan.FromSeconds(3);

    /// <summary>Campos dos contratos v1 (DOMINIO.md, seção 7). Não dependem de nenhum nome do Domínio.</summary>
    private static readonly string[] CamposDeContaAberta = ["tipoEvento", "versao", "eventoId", "contaId", "titularId", "sequencia", "moeda", "dataAbertura", "correlacaoId"];

    private static readonly string[] CamposDeMovimentacao =
        ["tipoEvento", "versao", "eventoId", "contaId", "lancamentoId", "sequencia", "tipo", "valor", "moeda", "dataCompetencia", "dataRegistro", "lancamentoEstornadoId", "correlacaoId"];

    private string Conta => (string)contexto.Memoria["conta"];

    private async Task<EventoRecebido> EventoAsync(string contrato, Func<EventoRecebido, bool> filtro, string descricao)
    {
        EventoRecebido? evento = null;
        await Esperar.AteAsync(
            () =>
            {
                evento = contexto.Eventos.Da(contrato, Conta).FirstOrDefault(filtro);
                return Task.FromResult(evento is not null);
            },
            descricao);
        return evento!;
    }

    [Then(@"^Lançamentos avisa uma única vez que a conta foi aberta, informando a conta, o titular e a sequência 0$")]
    public async Task ContaAbertaPublicada()
    {
        var evento = await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        evento.Texto("versao").Should().Be("1");
        evento.Texto("contaId").Should().Be(Conta);
        evento.Texto("titularId").Should().Be(contexto.Titular);
        evento.Texto("sequencia").Should().Be("0");
        await Esperar.NuncaAsync(() => Task.FromResult(contexto.Eventos.Da("ContaAbertaV1", Conta).Count > 1), "segundo ContaAberta", JanelaSemEvento);
    }

    [When(@"^o cliente repete o mesmo pedido de abertura$")]
    public async Task AberturaRepetida()
    {
        contexto.Memoria["conta"] = await contexto.AbrirContaAsync(chave: "abertura-1");
        await contexto.AbrirContaAsync(chave: "abertura-1");
    }

    [Then(@"^Lançamentos não avisa a abertura de novo$")]
    public async Task UmaUnicaAbertura()
    {
        await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        await Esperar.NuncaAsync(() => Task.FromResult(contexto.Eventos.Da("ContaAbertaV1", Conta).Count > 1), "segundo ContaAberta", JanelaSemEvento);
    }

    [Given(@"^uma conta com dois lançamentos e saldo de R\$ 100,00$")]
    public async Task ContaComDoisLancamentos()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 110m);
        await contexto.DebitarAsync(conta, 10m);
    }

    [Then(@"^Lançamentos avisa a movimentação: débito de R\$ 30,00, terceiro lançamento da conta, sem lançamento estornado$")]
    public async Task DebitoPublicado()
    {
        var evento = await EventoAsync("MovimentacaoRegistradaV1", e => e.Texto("sequencia") == "3", "movimentação da sequência 3");
        evento.Texto("versao").Should().Be("1");
        evento.Texto("tipo").Should().Be("Debito");
        // O MassTransit serializa decimal como texto, sem perder as casas.
        (decimal.Parse(evento.Texto("valor"), CultureInfo.InvariantCulture)).Should().Be(30.00m);
        evento.Mensagem.GetProperty("lancamentoEstornadoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Then(@"^Lançamentos avisa a movimentação como estorno, indicando o débito estornado$")]
    public async Task EstornoPublicado()
    {
        Linguagem.ConferirRegistro(contexto.UltimaResposta);
        var evento = await EventoAsync("MovimentacaoRegistradaV1", e => e.Texto("tipo") == "Estorno", "movimentação de estorno");
        evento.Texto("lancamentoEstornadoId").Should().Be((string)contexto.Memoria["débito"]);
    }

    [When(@"^um débito é recusado por saldo insuficiente$")]
    public async Task DebitoRecusado()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        Linguagem.ConferirRecusa(await contexto.LancarAsync(conta, "debitos", new { valor = 10m, descricao = "Débito" }), "saldo insuficiente");
    }

    [When(@"^a gravação de um crédito falha antes de ser concluída$")]
    public async Task GravacaoFalha()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        await contexto.TornarBancoDeLancamentosIndisponivelAsync();
        try
        {
            (await contexto.LancarAsync(conta, "creditos", new { valor = 10m, descricao = "Crédito" })).Status.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await contexto.TornarBancoDeLancamentosDisponivelAsync();
        }
    }

    [Then(@"^o crédito não fica registrado$")]
    public async Task CreditoNaoRegistrado() => (await contexto.ContarLancamentosAsync(Conta)).Should().Be(0L);

    [Then(@"^Lançamentos não avisa nenhuma movimentação$")]
    public async Task NenhumaMovimentacao() =>
        await Esperar.NuncaAsync(() => Task.FromResult(contexto.Eventos.Da("MovimentacaoRegistradaV1", Conta).Count > 0), "movimentação publicada", JanelaSemEvento);

    [When(@"^a entrega de avisos entre os contextos está indisponível e o titular credita R\$ 25,00$")]
    public async Task CreditoComBrokerFora()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        await contexto.PararBrokerAsync();
        var resposta = await contexto.LancarAsync(conta, "creditos", new { valor = 25m, descricao = "Crédito com broker fora" });
        contexto.Memoria["lançamento"] = resposta.Status == HttpStatusCode.Created ? resposta.Texto("lancamentoId") : string.Empty;
    }

    [Then(@"^o crédito é registrado normalmente$")]
    public void CreditoRegistrado() => Linguagem.ConferirRegistro(contexto.UltimaResposta);

    [Then(@"^quando a entrega volta, Lançamentos avisa a movimentação desse crédito$")]
    public async Task PublicadoQuandoOBrokerVolta()
    {
        await contexto.SubirBrokerAsync();
        var lancamento = (string)contexto.Memoria["lançamento"];
        await EventoAsync("MovimentacaoRegistradaV1", e => e.Texto("lancamentoId") == lancamento, "movimentação entregue depois da volta do broker");
    }

    [When(@"^o mesmo aviso de movimentação é entregue mais de uma vez$")]
    public async Task EntregueDuasVezes()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;

        // Uma fila própria recusa a primeira entrega com devolução, e o broker entrega a mesma mensagem de novo.
        await using var conexao = await contexto.ConectarAoBrokerAsync();
        await using var canal = await conexao.CreateChannelAsync();
        var fila = $"reentrega-{Guid.NewGuid():N}";
        await canal.QueueDeclareAsync(fila, durable: false, exclusive: true, autoDelete: true);
        await canal.ExchangeDeclareAsync("Contratos.Integracao.Lancamentos:MovimentacaoRegistradaV1", ExchangeType.Fanout, durable: true);
        await canal.QueueBindAsync(fila, "Contratos.Integracao.Lancamentos:MovimentacaoRegistradaV1", string.Empty);
        await contexto.CreditarAsync(conta, 10m);

        var entregas = new List<(string EventoId, bool Reentregue)>();
        await Esperar.AteAsync(
            async () =>
            {
                var mensagem = await canal.BasicGetAsync(fila, autoAck: false);
                if (mensagem is null)
                {
                    return false;
                }

                using var envelope = JsonDocument.Parse(mensagem.Body);
                entregas.Add((envelope.RootElement.GetProperty("message").GetProperty("eventoId").GetString()!, mensagem.Redelivered));
                if (entregas.Count == 1)
                {
                    await canal.BasicNackAsync(mensagem.DeliveryTag, multiple: false, requeue: true);
                    return false;
                }

                await canal.BasicAckAsync(mensagem.DeliveryTag, multiple: false);
                return true;
            },
            "duas entregas da mesma movimentação");
        contexto.Memoria["entregas"] = entregas;
    }

    [Then(@"^todas as entregas têm a mesma identificação, para que quem recebe possa descartar as repetições$")]
    public void MesmoEventoId()
    {
        var entregas = (List<(string EventoId, bool Reentregue)>)contexto.Memoria["entregas"];
        entregas.Count.Should().Be(2);
        entregas[1].Reentregue.Should().BeTrue();
        entregas.Select(e => e.EventoId).Distinct().Should().ContainSingle();
    }

    [When(@"^Lançamentos avisa uma movimentação$")]
    public async Task MovimentacaoPublicada()
    {
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        contexto.Memoria["descrição"] = $"Descricao-sigilosa-{Guid.NewGuid():N}";
        await contexto.LancarAsync(conta, "creditos", new { valor = 10m, descricao = (string)contexto.Memoria["descrição"] });
    }

    [Then(@"^o aviso não identifica o titular nem traz a descrição do lançamento$")]
    public async Task SemTitularNemDescricao()
    {
        var evento = await EventoAsync("MovimentacaoRegistradaV1", _ => true, "movimentação publicada");
        evento.Mensagem.TryGetProperty("titularId", out _).Should().BeFalse();
        evento.Mensagem.ToString().Should().NotContain((string)contexto.Memoria["descrição"]);
        evento.Mensagem.ToString().Should().NotContain(contexto.Titular);
    }

    [When(@"^o modelo interno de Lançamentos evolui$")]
    public async Task DominioRenomeado()
    {
        // Não dá para renomear o Domínio durante a execução. O que garante a regra é o evento publicado ter exatamente
        // os campos do contrato v1, que é um tipo separado do Domínio: mudar um nome interno não muda esses campos.
        var conta = await contexto.AbrirContaAsync();
        contexto.Memoria["conta"] = conta;
        await contexto.CreditarAsync(conta, 10m);
    }

    [Then(@"^os avisos continuam com as mesmas informações da versão 1$")]
    public async Task ContratoIgual()
    {
        var abertura = await EventoAsync("ContaAbertaV1", _ => true, "ContaAberta publicado");
        var movimentacao = await EventoAsync("MovimentacaoRegistradaV1", _ => true, "movimentação publicada");
        abertura.Mensagem.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).Should().Equal(CamposDeContaAberta.Order(StringComparer.Ordinal));
        movimentacao.Mensagem.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).Should().Equal(CamposDeMovimentacao.Order(StringComparer.Ordinal));
    }
}
