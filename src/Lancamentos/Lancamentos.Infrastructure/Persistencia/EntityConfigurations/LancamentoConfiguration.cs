namespace Lancamentos.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Mapeamento da Entity <see cref="Lancamento"/> na tabela "lancamentos".</summary>
internal sealed class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("lancamentos");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id").HasConversion(ValueConverters.LancamentoId);
        builder.Property(l => l.ContaId).HasColumnName("conta_id").HasConversion(ValueConverters.ContaId);
        builder.Property(l => l.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Valor).HasColumnName("valor").HasConversion(ValueConverters.Valor).HasPrecision(14, 2);
        builder.Property(l => l.Descricao).HasColumnName("descricao").HasConversion(ValueConverters.Descricao).HasMaxLength(Descricao.TamanhoMaximo);
        builder.Property(l => l.DataCompetencia).HasColumnName("data_competencia");
        builder.Property(l => l.DataRegistro).HasColumnName("data_registro");
        builder.Property(l => l.Sequencia).HasColumnName("sequencia");
        builder.Property(l => l.LancamentoEstornadoId).HasColumnName("lancamento_estornado_id").HasConversion(ValueConverters.LancamentoIdOpcional);
        builder.Property(l => l.ChaveIdempotencia).HasColumnName("chave_idempotencia").HasConversion(ValueConverters.Chave).HasMaxLength(ChaveIdempotencia.TamanhoMaximo);
        builder.Property(l => l.SaldoDisponivelApos).HasColumnName("saldo_disponivel_apos").HasPrecision(14, 2);

        builder.HasOne<Conta>().WithMany().HasForeignKey(l => l.ContaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ContaId, l.Sequencia }).IsUnique().HasDatabaseName(LancamentosDbContext.Indices.LancamentoPorContaESequencia);
        builder.HasIndex(l => new { l.ContaId, l.ChaveIdempotencia }).IsUnique().HasDatabaseName(LancamentosDbContext.Indices.LancamentoPorContaEChave);
        builder.HasIndex(l => l.LancamentoEstornadoId)
            .IsUnique()
            .HasFilter("lancamento_estornado_id IS NOT NULL")
            .HasDatabaseName(LancamentosDbContext.Indices.EstornoPorLancamento);
    }
}
