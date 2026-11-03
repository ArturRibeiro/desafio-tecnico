namespace Posicao.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Mapeamento da Entity <see cref="Movimento"/> na tabela "movimentos".</summary>
internal sealed class MovimentoConfiguration : IEntityTypeConfiguration<Movimento>
{
    public void Configure(EntityTypeBuilder<Movimento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("movimentos");
        builder.HasKey(m => new { m.ContaId, m.Sequencia });
        builder.Ignore(m => m.Sinalizado);

        builder.Property(m => m.ContaId).HasColumnName("conta_id").HasConversion(ValueConverters.ContaId);
        builder.Property(m => m.Sequencia).HasColumnName("sequencia");
        builder.Property(m => m.LancamentoId).HasColumnName("lancamento_id").HasConversion(ValueConverters.LancamentoId);
        builder.Property(m => m.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Valor).HasColumnName("valor").HasConversion(ValueConverters.Valor).HasPrecision(14, 2);
        builder.Property(m => m.DataCompetencia).HasColumnName("data_competencia");
        builder.Property(m => m.DataRegistro).HasColumnName("data_registro");
        builder.Property(m => m.LancamentoEstornadoId).HasColumnName("lancamento_estornado_id").HasConversion(ValueConverters.LancamentoIdOpcional);
        builder.Property(m => m.SaldoInformadoApos).HasColumnName("saldo_informado_apos").HasPrecision(14, 2);
        builder.Property(m => m.Efeito).HasColumnName("efeito").HasConversion<string>().HasMaxLength(20);

        // Sem chave estrangeira para titularidades: o movimento pode chegar antes da abertura e ficar pendente.
        builder.HasIndex(m => m.LancamentoId).IsUnique().HasDatabaseName("ux_movimentos_lancamento");
        builder.HasIndex(m => new { m.ContaId, m.DataCompetencia }).HasDatabaseName("ix_movimentos_conta_competencia");
    }
}
