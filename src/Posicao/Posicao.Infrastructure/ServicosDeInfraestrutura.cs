namespace Posicao.Infrastructure;

public static class ServicosDeInfraestrutura
{
    /// <summary>Banco, repositórios, leitura das Queries (IDbContextReadOnly), unidade de trabalho e serviços dos casos de uso de Posição.</summary>
    public static IServiceCollection AdicionarInfraestruturaDePosicao(this IServiceCollection servicos, string postgres, IConfiguration secaoBanco)
    {
        servicos.AddDbContext<PosicaoDbContext>(o => o.UseNpgsql(postgres));
        servicos.AddScoped<ITitularidadeRepository, TitularidadeRepository>();
        servicos.AddScoped<IMovimentoRepository, MovimentoRepository>();
        servicos.AddScoped<IFechamentoDiarioRepository, FechamentoDiarioRepository>();
        servicos.AddScoped<IDbContextReadOnly, DbContextReadOnly>();
        servicos.AddScoped<IUnitOfWork, UnitOfWork>();
        servicos.AddSingleton(TimeProvider.System);

        servicos.AddScoped<AplicacaoDePendentes>();
        servicos.AddMetrics();
        servicos.AddSingleton<IRegistroDeDivergencias, DivergenciaLogAdapter>();

        servicos.Configure<BancoOptions>(secaoBanco);
        return servicos;
    }

    /// <summary>
    /// Cache Redis com timeouts curtos e sem abortar a conexão: fora do ar, cada operação falha rápido e a consulta
    /// segue pelo banco. Sem connection string, o cache fica desligado.
    /// </summary>
    public static IServiceCollection AdicionarSaldoCache(this IServiceCollection servicos, string? redis)
    {
        if (string.IsNullOrWhiteSpace(redis))
        {
            servicos.AddSingleton<ISaldoCache, NullSaldoCache>();
            return servicos;
        }

        var opcoes = ConfigurationOptions.Parse(redis);
        opcoes.AbortOnConnectFail = false;
        opcoes.ConnectTimeout = 250;
        opcoes.SyncTimeout = 250;
        opcoes.AsyncTimeout = 250;
        servicos.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(opcoes));
        servicos.AddSingleton<ISaldoCache, SaldoRedisCache>();
        return servicos;
    }

    /// <summary>
    /// Inbox do MassTransit no banco de Posição: o MessageId (o eventoId de Lançamentos) já processado é descartado,
    /// e o registro do Inbox é gravado na mesma transação da projeção. ReadCommitted, para que cada consulta feita
    /// depois do lock da conta enxergue o que a transação anterior da mesma conta confirmou.
    /// </summary>
    public static void AdicionarInboxDePosicao(this IBusRegistrationConfigurator barramento)
    {
        ArgumentNullException.ThrowIfNull(barramento);

        barramento.AddEntityFrameworkOutbox<PosicaoDbContext>(o =>
        {
            o.UsePostgres();
            o.IsolationLevel = IsolationLevel.ReadCommitted;
        });
    }

    public static void UsarInboxDePosicao(this IReceiveEndpointConfigurator endpoint, IRegistrationContext contexto) =>
        endpoint.UseEntityFrameworkOutbox<PosicaoDbContext>(contexto);

    public static IServiceCollection AdicionarMigracaoHostedService(this IServiceCollection servicos) =>
        servicos.AddHostedService<MigracaoHostedService>();

    public static IHealthChecksBuilder AdicionarMigracaoHealthCheck(this IHealthChecksBuilder saude, params string[] tags) =>
        saude.AddCheck<MigracaoHealthCheck>("migracoes", tags: tags);
}
