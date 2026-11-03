namespace Posicao.Infrastructure.Persistencia;

public sealed class PosicaoDbContext(DbContextOptions<PosicaoDbContext> opcoes) : DbContext(opcoes)
{
    public DbSet<Titularidade> Titularidades => Set<Titularidade>();

    public DbSet<Movimento> Movimentos => Set<Movimento>();

    public DbSet<FechamentoDiario> Fechamentos => Set<FechamentoDiario>();

    /// <summary>Função <c>bloquear_conta</c> do banco, consultada em LINQ pelo TitularidadeRepository.</summary>
    internal IQueryable<BloqueioDaConta> BloquearConta(Guid contaId) => FromExpression(() => BloquearConta(contaId));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Um IEntityTypeConfiguration por entidade, na pasta EntityConfigurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosicaoDbContext).Assembly);
        modelBuilder.HasDbFunction(typeof(PosicaoDbContext).GetMethod(nameof(BloquearConta), BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Guid)])!)
            .HasName("bloquear_conta");

        // Tabelas do Inbox do MassTransit (e do Outbox, que o Inbox usa), na mesma transação da projeção.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
