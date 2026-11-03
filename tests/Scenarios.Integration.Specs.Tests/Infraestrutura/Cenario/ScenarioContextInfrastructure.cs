namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Cenario;

/// <summary>
/// Infraestrutura de um cenário: os seus bancos, o seu virtual host no RabbitMQ, o Redis limpo, o seu relógio e os
/// serviços no processo. É criada em <see cref="IniciarAsync"/>, chamado pelo <see cref="Hooks"/> antes do cenário, e
/// descartada em <see cref="DisposeAsync"/>, depois dele.
/// </summary>
public sealed class ScenarioContextInfrastructure : IAsyncDisposable
{
    private const string RedisInacessivel = "127.0.0.1:1,connectTimeout=250,syncTimeout=250";

    private readonly string id = Guid.NewGuid().ToString("N")[..12];
    private ServicoDeLancamentos? lancamentos;
    private ServicoDePosicao? posicao;
    private ServicoDoWorker? worker;
    private HttpClient? clienteDeLancamentos;
    private HttpClient? clienteDePosicao;
    private string? bancoDeLancamentos;
    private string? bancoDePosicao;
    private bool virtualHostCriado;
    private bool brokerParado;
    private SondaDeEventos? sonda;

    /// <summary>Divergências de saldo registradas pelo Posicao.Worker do cenário.</summary>
    public DivergenciasDeSaldo Divergencias { get; } = new();

    /// <summary>Relógio de todos os serviços do cenário: começa no instante real e avança um pouco a cada leitura.</summary>
    public FakeTimeProvider Relogio { get; } = new(DateTimeOffset.UtcNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(5) };

    /// <summary>DbContext da Lancamentos.Api do cenário.</summary>
    public LancamentosDbContext LancamentosDbContext => (lancamentos ?? throw NaoIniciado()).DbContext;

    /// <summary>DbContext da Posicao.Api do cenário.</summary>
    public PosicaoDbContext PosicaoDbContext => (posicao ?? throw NaoIniciado()).DbContext;

    /// <summary>Eventos publicados por Lançamentos neste cenário.</summary>
    public SondaDeEventos Eventos => sonda ?? throw NaoIniciado();

    public HttpClient Lancamentos => clienteDeLancamentos ?? throw NaoIniciado();

    public HttpClient Posicao => clienteDePosicao ?? throw NaoIniciado();

    private string VirtualHost => $"vh_{id}";

    private string NomeDoBancoDeLancamentos => $"lanc_{id}";

    private string NomeDoBancoDePosicao => $"pos_{id}";

    private string PrefixoDasFilas => $"posicao-{id}";

    private string ConexaoDeLancamentos => bancoDeLancamentos ?? throw NaoIniciado();

    private string ConexaoDePosicao => bancoDePosicao ?? throw NaoIniciado();

    /// <summary>
    /// Cria os bancos e o virtual host, limpa o Redis e sobe Posicao.Worker, Lancamentos.Api e Posicao.Api. Como no
    /// ambiente real, o Worker já está consumindo quando Lançamentos publica.
    /// </summary>
    public async Task IniciarAsync()
    {
        var bancos = Task.WhenAll(ContainerPostgres.CriarBancoAsync(NomeDoBancoDeLancamentos), ContainerPostgres.CriarBancoAsync(NomeDoBancoDePosicao));
        await Task.WhenAll(bancos, ContainerRabbitMq.CriarVirtualHostAsync(VirtualHost), ContainerRedis.LimparAsync());
        (bancoDeLancamentos, bancoDePosicao) = (bancos.Result[0], bancos.Result[1]);
        virtualHostCriado = true;

        // Um serviço por vez: o WebApplicationFactory não sobe dois hosts em paralelo com segurança. As migrações de
        // Posição são aplicadas pelo Worker; a Posicao.Api só verifica o esquema.
        await SubirWorkerAsync();
        await SubirLancamentosAsync();
        await SubirPosicaoAsync(ContainerRedis.ConnectionString);
    }

    /// <summary>Sobe a Posicao.Api de novo, com o Redis inacessível.</summary>
    public async Task DerrubarCacheAsync()
    {
        clienteDePosicao?.Dispose();
        if (posicao is not null)
        {
            await posicao.DisposeAsync();
        }

        await SubirPosicaoAsync(RedisInacessivel);
    }

    /// <summary>Inicia (ou retoma) o consumo de eventos.</summary>
    public Task IniciarWorkerAsync() => SubirWorkerAsync();

    /// <summary>Para o consumo de eventos; ele só volta com <see cref="IniciarWorkerAsync"/>.</summary>
    public async Task PararWorkerAsync()
    {
        if (worker is not null)
        {
            await worker.DisposeAsync();
            worker = null;
        }
    }

    /// <summary>O mesmo que o comando "reconstruir" do Posicao.Worker faz, pelo Mediator do Worker do cenário.</summary>
    public Task<int> ReconstruirAsync() =>
        (worker ?? throw new InvalidOperationException("O Posicao.Worker está parado.")).ReconstruirAsync();

    public async Task PublicarAsync(string contrato, JsonObject mensagem, Guid? messageId = null) =>
        await Broker.PublicarAsync(VirtualHost, contrato, mensagem, messageId);

    public async Task PararBrokerAsync()
    {
        brokerParado = true;
        await ContainerRabbitMq.PararAsync();
    }

    public async Task SubirBrokerAsync()
    {
        await ContainerRabbitMq.SubirAsync();
        brokerParado = false;
    }

    /// <summary>
    /// O banco de Lançamentos passa a recusar conexões e as abertas são derrubadas: uma falha técnica real. O pool é
    /// limpo para a próxima operação já encontrar o banco fora.
    /// </summary>
    public async Task TornarBancoDeLancamentosIndisponivelAsync()
    {
        await ContainerPostgres.AlterarDisponibilidadeDoBancoAsync(NomeDoBancoDeLancamentos, disponivel: false);
        NpgsqlConnection.ClearAllPools();
    }

    public Task TornarBancoDeLancamentosDisponivelAsync() =>
        ContainerPostgres.AlterarDisponibilidadeDoBancoAsync(NomeDoBancoDeLancamentos, disponivel: true);

    /// <summary>O banco de Posição passa a recusar conexões e as abertas são derrubadas.</summary>
    public Task TornarBancoDePosicaoIndisponivelAsync() =>
        ContainerPostgres.AlterarDisponibilidadeDoBancoAsync(NomeDoBancoDePosicao, disponivel: false);

    public Task TornarBancoDePosicaoDisponivelAsync() =>
        ContainerPostgres.AlterarDisponibilidadeDoBancoAsync(NomeDoBancoDePosicao, disponivel: true);

    /// <summary>Mensagens na fila de falhas (_error) do <paramref name="consumidor"/> do Posicao.Worker, como "movimentacao-registrada".</summary>
    public Task<uint> ContarMensagensComFalhaAsync(string consumidor) =>
        Broker.ContarMensagensAsync(VirtualHost, $"{PrefixoDasFilas}-{consumidor}_error");

    /// <summary>Conexão própria com o virtual host do cenário, para os cenários que inspecionam a entrega no broker.</summary>
    public Task<IConnection> ConectarAoBrokerAsync() =>
        new ConnectionFactory { Uri = new Uri(ContainerRabbitMq.Uri(VirtualHost)) }.CreateConnectionAsync();

    public async ValueTask DisposeAsync()
    {
        clienteDeLancamentos?.Dispose();
        clienteDePosicao?.Dispose();
        if (lancamentos is not null)
        {
            await lancamentos.DisposeAsync();
        }

        if (posicao is not null)
        {
            await posicao.DisposeAsync();
        }

        await PararWorkerAsync();
        if (sonda is not null)
        {
            await sonda.DisposeAsync();
        }

        if (brokerParado)
        {
            await SubirBrokerAsync();
        }

        NpgsqlConnection.ClearAllPools();
        if (bancoDeLancamentos is not null)
        {
            await ContainerPostgres.ApagarBancoAsync(NomeDoBancoDeLancamentos);
        }

        if (bancoDePosicao is not null)
        {
            await ContainerPostgres.ApagarBancoAsync(NomeDoBancoDePosicao);
        }

        if (virtualHostCriado)
        {
            await ContainerRabbitMq.ApagarVirtualHostAsync(VirtualHost);
        }
    }

    private async Task SubirLancamentosAsync()
    {
        sonda = await SondaDeEventos.CriarAsync(ContainerRabbitMq.Uri(VirtualHost), $"sonda-{id}");
        lancamentos = new ServicoDeLancamentos(ConexaoDeLancamentos, VirtualHost, Relogio);
        clienteDeLancamentos = lancamentos.CreateClient();
        await EsperarProntoAsync(clienteDeLancamentos, "Lancamentos.Api");
    }

    private async Task SubirPosicaoAsync(string redis)
    {
        posicao = new ServicoDePosicao(ConexaoDePosicao, redis, Relogio);
        clienteDePosicao = posicao.CreateClient();
        await EsperarProntoAsync(clienteDePosicao, "Posicao.Api");
    }

    private async Task SubirWorkerAsync()
    {
        if (worker is not null)
        {
            return;
        }

        worker = new ServicoDoWorker(ConexaoDePosicao, VirtualHost, ContainerRedis.ConnectionString, PrefixoDasFilas, Relogio, Divergencias);
        var cliente = worker.CreateClient();
        if (!brokerParado)
        {
            await EsperarProntoAsync(cliente, "Posicao.Worker");
        }
    }

    private static InvalidOperationException NaoIniciado() => new("O cenário ainda não foi iniciado pelo Hooks.");

    private static async Task EsperarProntoAsync(HttpClient cliente, string servico) =>
        await Esperar.AteAsync(
            async () => (await cliente.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode == HttpStatusCode.OK,
            $"{servico} pronto");
}
