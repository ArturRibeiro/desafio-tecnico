namespace Lancamentos.Infrastructure;

public static class ServicosDeInfraestrutura
{
    /// <summary>Banco, repositórios, unidade de trabalho, publicação de eventos e migrações de Lançamentos.</summary>
    public static IServiceCollection AdicionarInfraestruturaDeLancamentos(this IServiceCollection servicos, string postgres, IConfiguration secaoBanco)
    {
        servicos.AddDbContext<LancamentosDbContext>(o => o.UseNpgsql(postgres).AddInterceptors(new BloqueioParaAlteracaoInterceptor()));
        servicos.AddScoped<IContaRepository, ContaRepository>();
        servicos.AddScoped<ILancamentoRepository, LancamentoRepository>();
        servicos.AddScoped<IUnitOfWork, UnitOfWork>();
        servicos.AddScoped<EventoPublisher>();

        servicos.Configure<BancoOptions>(secaoBanco);
        servicos.AddHostedService<MigracaoHostedService>();
        return servicos;
    }

    /// <summary>
    /// Transactional Outbox: as publicações viram linhas em OutboxMessage no mesmo SaveChanges, e o Delivery
    /// Service (dentro desta API) as entrega ao broker, com lock no banco entre instâncias.
    /// A entrega roda em ReadCommitted: a reserva de cada mensagem já é exclusiva (FOR UPDATE SKIP LOCKED), e o
    /// RepeatableRead padrão fazia os ciclos de entrega concorrentes falharem por serialização (40001) e esperarem a
    /// retentativa, limitando a entrega a uns 2 eventos/s no teste de carga.
    /// </summary>
    public static void AdicionarOutboxDeLancamentos(this IBusRegistrationConfigurator barramento)
    {
        ArgumentNullException.ThrowIfNull(barramento);

        barramento.AddEntityFrameworkOutbox<LancamentosDbContext>(o =>
        {
            o.UsePostgres();
            o.IsolationLevel = IsolationLevel.ReadCommitted;
            o.UseBusOutbox();
        });
    }

    public static IHealthChecksBuilder AdicionarMigracaoHealthCheck(this IHealthChecksBuilder saude, params string[] tags) =>
        saude.AddCheck<MigracaoHealthCheck>("migracoes", tags: tags);
}
