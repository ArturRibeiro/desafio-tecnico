// Host web só para expor a saúde: o Worker não tem rotas de negócio.
var builder = WebApplication.CreateBuilder(args);
builder.AdicionarObservabilidade("posicao-worker", "MassTransit", "Npgsql");
var reconstruir = args.Contains("reconstruir", StringComparer.Ordinal);

var postgres = builder.Configuration.ConnectionStringObrigatoria("Postgres");
builder.Services.AdicionarInfraestruturaDePosicao(postgres, builder.Configuration.GetSection("Banco"));
builder.Services.AdicionarMediator(typeof(ProjetarMovimentacaoCommand).Assembly);
// O Worker só usa o cache para limpá-lo na reconstrução; sem Redis configurado, fica desligado.
builder.Services.AdicionarCacheDeSaldos(builder.Configuration.GetConnectionString("Redis"));

if (reconstruir)
{
    // dotnet Posicao.Worker.dll reconstruir: recalcula os fechamentos e encerra, sem consumir eventos.
    using var host = builder.Build();
    await using var escopo = host.Services.CreateAsyncScope();
    var contas = await escopo.ServiceProvider.GetRequiredService<IMediator>().SendAsync(new ReconstruirPosicaoCommand(), CancellationToken.None);
    Console.WriteLine($"Posição reconstruída: {contas} conta(s).");
    return;
}

builder.Services.AdicionarAplicadorDeMigracoes();
builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgres", tags: [Saude.TagProntidao], timeout: TimeSpan.FromSeconds(3))
    .AdicionarVerificacaoDeMigracoes(Saude.TagProntidao);

var consumo = builder.Configuration.GetSection("Consumo").Get<OpcoesDeConsumo>() ?? new OpcoesDeConsumo();
var rabbitMq = new Uri(builder.Configuration.ConnectionStringObrigatoria("RabbitMq"));
builder.Services.AddMassTransit(x =>
{
    x.ConfigureHealthCheckOptions(o =>
    {
        o.Name = "rabbitmq";
        o.Tags.Add(Saude.TagProntidao);
    });
    x.AddConsumer<ConsumidorDeContaAberta>();
    x.AddConsumer<ConsumidorDeMovimentacaoRegistrada>();
    x.AdicionarInboxDePosicao();
    x.UsingRabbitMq((contexto, cfg) =>
    {
        cfg.Host(rabbitMq);
        cfg.ReceiveEndpoint($"{consumo.PrefixoDasFilas}-conta-aberta", e => Configurar<ConsumidorDeContaAberta>(e, contexto));
        cfg.ReceiveEndpoint($"{consumo.PrefixoDasFilas}-movimentacao-registrada", e => Configurar<ConsumidorDeMovimentacaoRegistrada>(e, contexto));
    });
});

var app = builder.Build();

app.MapearSaude();

await app.RunAsync();

// Retentativa por fora do Inbox: cada tentativa é uma transação nova. Esgotadas as tentativas, o MassTransit move a
// mensagem para a fila <endpoint>_error. Um evento inválido não é tentado de novo.
void Configurar<TConsumidor>(IRabbitMqReceiveEndpointConfigurator endpoint, IBusRegistrationContext contexto)
    where TConsumidor : class, IConsumer
{
    endpoint.ConcurrentMessageLimit = consumo.LimiteDeMensagensConcorrentes;
    endpoint.UseMessageRetry(r =>
    {
        r.Exponential(consumo.Retentativas, consumo.IntervaloMinimo, consumo.IntervaloMaximo, consumo.IntervaloMinimo);
        r.Ignore<EventoInvalidoException>();
    });
    endpoint.UsarInboxDePosicao(contexto);
    endpoint.ConfigureConsumer<TConsumidor>(contexto);
}
