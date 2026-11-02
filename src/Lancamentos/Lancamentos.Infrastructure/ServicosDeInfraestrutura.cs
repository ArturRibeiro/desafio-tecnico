namespace Lancamentos.Infrastructure;

public static class ServicosDeInfraestrutura
{
    /// <summary>Banco, repositórios, unidade de trabalho, publicação de eventos e migrações de Lançamentos.</summary>
    public static IServiceCollection AdicionarInfraestruturaDeLancamentos(this IServiceCollection servicos, string postgres, IConfiguration secaoBanco)
    {
        servicos.AddDbContext<LancamentosDbContext>(o => o.UseNpgsql(postgres));
        servicos.AddScoped<IRepositorioDeContas, RepositorioDeContas>();
        servicos.AddScoped<IRepositorioDeLancamentos, RepositorioDeLancamentos>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        servicos.AddScoped<PublicadorDeEventos>();

        servicos.Configure<OpcoesDeBanco>(secaoBanco);
        servicos.AddHostedService<AplicadorDeMigracoes>();
        return servicos;
    }

    /// <summary>
    /// Transactional Outbox: as publicações viram linhas em OutboxMessage no mesmo SaveChanges, e o Delivery
    /// Service (dentro desta API) as entrega ao broker, com lock no banco entre instâncias.
    /// </summary>
    public static void AdicionarOutboxDeLancamentos(this IBusRegistrationConfigurator barramento)
    {
        ArgumentNullException.ThrowIfNull(barramento);

        barramento.AddEntityFrameworkOutbox<LancamentosDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });
    }

    public static IHealthChecksBuilder AdicionarVerificacaoDeMigracoes(this IHealthChecksBuilder saude, params string[] tags) =>
        saude.AddCheck<VerificacaoDeMigracoes>("migracoes", tags: tags);
}
