namespace Lancamentos.Infrastructure.Persistencia;

public sealed class LancamentosDbContext(DbContextOptions<LancamentosDbContext> opcoes) : DbContext(opcoes)
{
    /// <summary>Nomes dos índices únicos: a tradução das violações (23505) para falhas de negócio depende deles.</summary>
    public static class Indices
    {
        public const string ContaPorTitularEChaveDeAbertura = "ux_contas_titular_chave_abertura";
        public const string LancamentoPorContaESequencia = "ux_lancamentos_conta_sequencia";
        public const string LancamentoPorContaEChave = "ux_lancamentos_conta_chave_idempotencia";
        public const string EstornoPorLancamento = "ux_lancamentos_lancamento_estornado";
    }

    public DbSet<Conta> Contas => Set<Conta>();

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Um IEntityTypeConfiguration por entidade, na pasta EntityConfigurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LancamentosDbContext).Assembly);

        // Tabelas do Transactional Outbox do MassTransit, no mesmo banco e na mesma transação dos lançamentos.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
