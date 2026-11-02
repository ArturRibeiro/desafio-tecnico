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

        modelBuilder.Entity<Conta>(MapearConta);
        modelBuilder.Entity<Lancamento>(MapearLancamento);

        // Tabelas do Transactional Outbox do MassTransit, no mesmo banco e na mesma transação dos lançamentos.
        modelBuilder.AddTransactionalOutboxEntities();
    }

    private static void MapearConta(EntityTypeBuilder<Conta> conta)
    {
        conta.ToTable("contas");
        conta.HasKey(c => c.Id);
        conta.Ignore(c => c.Eventos);

        conta.Property(c => c.Id).HasColumnName("id").HasConversion(Conversores.ContaId);
        conta.Property(c => c.Titular).HasColumnName("titular_id").HasConversion(Conversores.TitularId).HasMaxLength(TitularId.TamanhoMaximo);
        conta.Property(c => c.ChaveAbertura).HasColumnName("chave_abertura").HasConversion(Conversores.Chave).HasMaxLength(ChaveIdempotencia.TamanhoMaximo);
        conta.Property(c => c.SaldoDisponivel).HasColumnName("saldo_disponivel").HasPrecision(14, 2);
        conta.Property(c => c.Limite).HasColumnName("limite").HasPrecision(14, 2);
        conta.Property(c => c.Situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(20);
        conta.Property(c => c.UltimaSequencia).HasColumnName("ultima_sequencia");
        conta.Property(c => c.Versao).HasColumnName("versao").IsConcurrencyToken();
        conta.Property(c => c.DataAbertura).HasColumnName("data_abertura");

        conta.HasIndex(c => new { c.Titular, c.ChaveAbertura }).IsUnique().HasDatabaseName(Indices.ContaPorTitularEChaveDeAbertura);
    }

    private static void MapearLancamento(EntityTypeBuilder<Lancamento> lancamento)
    {
        lancamento.ToTable("lancamentos");
        lancamento.HasKey(l => l.Id);

        lancamento.Property(l => l.Id).HasColumnName("id").HasConversion(Conversores.LancamentoId);
        lancamento.Property(l => l.ContaId).HasColumnName("conta_id").HasConversion(Conversores.ContaId);
        lancamento.Property(l => l.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
        lancamento.Property(l => l.Valor).HasColumnName("valor").HasConversion(Conversores.Valor).HasPrecision(14, 2);
        lancamento.Property(l => l.Descricao).HasColumnName("descricao").HasConversion(Conversores.Descricao).HasMaxLength(Descricao.TamanhoMaximo);
        lancamento.Property(l => l.DataCompetencia).HasColumnName("data_competencia");
        lancamento.Property(l => l.DataRegistro).HasColumnName("data_registro");
        lancamento.Property(l => l.Sequencia).HasColumnName("sequencia");
        lancamento.Property(l => l.LancamentoEstornadoId).HasColumnName("lancamento_estornado_id").HasConversion(Conversores.LancamentoIdOpcional);
        lancamento.Property(l => l.ChaveIdempotencia).HasColumnName("chave_idempotencia").HasConversion(Conversores.Chave).HasMaxLength(ChaveIdempotencia.TamanhoMaximo);
        lancamento.Property(l => l.SaldoDisponivelApos).HasColumnName("saldo_disponivel_apos").HasPrecision(14, 2);

        lancamento.HasOne<Conta>().WithMany().HasForeignKey(l => l.ContaId).OnDelete(DeleteBehavior.Restrict);
        lancamento.HasIndex(l => new { l.ContaId, l.Sequencia }).IsUnique().HasDatabaseName(Indices.LancamentoPorContaESequencia);
        lancamento.HasIndex(l => new { l.ContaId, l.ChaveIdempotencia }).IsUnique().HasDatabaseName(Indices.LancamentoPorContaEChave);
        lancamento.HasIndex(l => l.LancamentoEstornadoId)
            .IsUnique()
            .HasFilter("lancamento_estornado_id IS NOT NULL")
            .HasDatabaseName(Indices.EstornoPorLancamento);
    }

    /// <summary>Conversões dos Value Objects. Os valores relidos do banco já foram validados na gravação.</summary>
    private static class Conversores
    {
        public static readonly ValueConverter<ContaId, Guid> ContaId = new(v => v.Valor, v => new ContaId(v));

        public static readonly ValueConverter<LancamentoId, Guid> LancamentoId = new(v => v.Valor, v => new LancamentoId(v));

        public static readonly ValueConverter<LancamentoId?, Guid?> LancamentoIdOpcional = new(
            v => v.HasValue ? v.Value.Valor : null,
            v => v.HasValue ? new LancamentoId(v.Value) : null);

        public static readonly ValueConverter<TitularId, string> TitularId = new(v => v.Valor, v => Domain.Comum.TitularId.Criar(v).Valor);

        public static readonly ValueConverter<ChaveIdempotencia, string> Chave = new(v => v.Valor, v => ChaveIdempotencia.Criar(v).Valor);

        public static readonly ValueConverter<Descricao, string> Descricao = new(v => v.Valor, v => Domain.Comum.Descricao.Criar(v).Valor);

        public static readonly ValueConverter<Valor, decimal> Valor = new(v => v.Quantia, v => Domain.Comum.Valor.Criar(v).Valor);
    }
}
