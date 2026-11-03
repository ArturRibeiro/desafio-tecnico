namespace Posicao.Infrastructure.Persistencia;

public sealed class PosicaoDbContext(DbContextOptions<PosicaoDbContext> opcoes) : DbContext(opcoes)
{
    public DbSet<Titularidade> Titularidades => Set<Titularidade>();

    public DbSet<Movimento> Movimentos => Set<Movimento>();

    public DbSet<FechamentoDiario> Fechamentos => Set<FechamentoDiario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Titularidade>(MapearTitularidade);
        modelBuilder.Entity<Movimento>(MapearMovimento);
        modelBuilder.Entity<FechamentoDiario>(MapearFechamento);

        // Tabelas do Inbox do MassTransit (e do Outbox, que o Inbox usa), na mesma transação da projeção.
        modelBuilder.AddTransactionalOutboxEntities();
    }

    private static void MapearTitularidade(EntityTypeBuilder<Titularidade> titularidade)
    {
        titularidade.ToTable("titularidades");
        titularidade.HasKey(t => t.ContaId);

        titularidade.Property(t => t.ContaId).HasColumnName("conta_id").HasConversion(Conversores.ContaId);
        titularidade.Property(t => t.Titular).HasColumnName("titular_id").HasConversion(Conversores.TitularId).HasMaxLength(TitularId.TamanhoMaximo);
        titularidade.Property(t => t.DataAbertura).HasColumnName("data_abertura");
        titularidade.Property(t => t.UltimaSequenciaAplicada).HasColumnName("ultima_sequencia_aplicada");
        titularidade.Property(t => t.AtualizadoAte).HasColumnName("atualizado_ate");
    }

    private static void MapearMovimento(EntityTypeBuilder<Movimento> movimento)
    {
        movimento.ToTable("movimentos");
        movimento.HasKey(m => new { m.ContaId, m.Sequencia });
        movimento.Ignore(m => m.Sinalizado);

        movimento.Property(m => m.ContaId).HasColumnName("conta_id").HasConversion(Conversores.ContaId);
        movimento.Property(m => m.Sequencia).HasColumnName("sequencia");
        movimento.Property(m => m.LancamentoId).HasColumnName("lancamento_id").HasConversion(Conversores.LancamentoId);
        movimento.Property(m => m.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
        movimento.Property(m => m.Valor).HasColumnName("valor").HasConversion(Conversores.Valor).HasPrecision(14, 2);
        movimento.Property(m => m.DataCompetencia).HasColumnName("data_competencia");
        movimento.Property(m => m.DataRegistro).HasColumnName("data_registro");
        movimento.Property(m => m.LancamentoEstornadoId).HasColumnName("lancamento_estornado_id").HasConversion(Conversores.LancamentoIdOpcional);
        movimento.Property(m => m.SaldoInformadoApos).HasColumnName("saldo_informado_apos").HasPrecision(14, 2);
        movimento.Property(m => m.Efeito).HasColumnName("efeito").HasConversion<string>().HasMaxLength(20);

        // Sem chave estrangeira para titularidades: o movimento pode chegar antes da abertura e ficar pendente.
        movimento.HasIndex(m => m.LancamentoId).IsUnique().HasDatabaseName("ux_movimentos_lancamento");
        movimento.HasIndex(m => new { m.ContaId, m.DataCompetencia }).HasDatabaseName("ix_movimentos_conta_competencia");
    }

    private static void MapearFechamento(EntityTypeBuilder<FechamentoDiario> fechamento)
    {
        fechamento.ToTable("fechamentos_diarios");
        fechamento.HasKey(f => new { f.ContaId, f.Data });

        fechamento.Property(f => f.ContaId).HasColumnName("conta_id").HasConversion(Conversores.ContaId);
        fechamento.Property(f => f.Data).HasColumnName("data").HasConversion(Conversores.DataFechamento);
        // Saldos e totais somam muitos lançamentos: mais dígitos que o valor de um lançamento (14,2).
        fechamento.Property(f => f.SaldoAbertura).HasColumnName("saldo_abertura").HasPrecision(19, 2);
        fechamento.Property(f => f.TotalCreditos).HasColumnName("total_creditos").HasPrecision(19, 2);
        fechamento.Property(f => f.TotalDebitos).HasColumnName("total_debitos").HasPrecision(19, 2);
        fechamento.Property(f => f.SaldoFechamento).HasColumnName("saldo_fechamento").HasPrecision(19, 2);
        fechamento.Property(f => f.UltimaSequenciaAplicada).HasColumnName("ultima_sequencia_aplicada");

        fechamento.HasOne<Titularidade>().WithMany().HasForeignKey(f => f.ContaId).OnDelete(DeleteBehavior.Restrict);
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

        public static readonly ValueConverter<Valor, decimal> Valor = new(v => v.Quantia, v => Domain.Comum.Valor.Criar(v).Valor);

        public static readonly ValueConverter<DataFechamento, DateOnly> DataFechamento = new(v => v.Valor, v => new DataFechamento(v));
    }
}
