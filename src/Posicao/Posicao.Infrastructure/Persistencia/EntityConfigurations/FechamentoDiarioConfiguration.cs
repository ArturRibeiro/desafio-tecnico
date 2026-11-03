namespace Posicao.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Mapeamento do Aggregate Root <see cref="FechamentoDiario"/> na tabela "fechamentos_diarios".</summary>
internal sealed class FechamentoDiarioConfiguration : IEntityTypeConfiguration<FechamentoDiario>
{
    public void Configure(EntityTypeBuilder<FechamentoDiario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fechamentos_diarios");
        builder.HasKey(f => new { f.ContaId, f.Data });

        builder.Property(f => f.ContaId).HasColumnName("conta_id").HasConversion(ValueConverters.ContaId);
        builder.Property(f => f.Data).HasColumnName("data").HasConversion(ValueConverters.DataFechamento);
        // Saldos e totais somam muitos lançamentos: mais dígitos que o valor de um lançamento (14,2).
        builder.Property(f => f.SaldoAbertura).HasColumnName("saldo_abertura").HasPrecision(19, 2);
        builder.Property(f => f.TotalCreditos).HasColumnName("total_creditos").HasPrecision(19, 2);
        builder.Property(f => f.TotalDebitos).HasColumnName("total_debitos").HasPrecision(19, 2);
        builder.Property(f => f.SaldoFechamento).HasColumnName("saldo_fechamento").HasPrecision(19, 2);
        builder.Property(f => f.UltimaSequenciaAplicada).HasColumnName("ultima_sequencia_aplicada");

        builder.HasOne<Titularidade>().WithMany().HasForeignKey(f => f.ContaId).OnDelete(DeleteBehavior.Restrict);
    }
}
