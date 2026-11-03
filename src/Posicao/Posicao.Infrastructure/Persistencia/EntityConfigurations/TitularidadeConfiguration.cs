namespace Posicao.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Mapeamento do Aggregate Root <see cref="Titularidade"/> na tabela "titularidades".</summary>
internal sealed class TitularidadeConfiguration : IEntityTypeConfiguration<Titularidade>
{
    public void Configure(EntityTypeBuilder<Titularidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("titularidades");
        builder.HasKey(t => t.ContaId);

        builder.Property(t => t.ContaId).HasColumnName("conta_id").HasConversion(ValueConverters.ContaId);
        builder.Property(t => t.Titular).HasColumnName("titular_id").HasConversion(ValueConverters.TitularId).HasMaxLength(TitularId.TamanhoMaximo);
        builder.Property(t => t.DataAbertura).HasColumnName("data_abertura");
        builder.Property(t => t.UltimaSequenciaAplicada).HasColumnName("ultima_sequencia_aplicada");
        builder.Property(t => t.AtualizadoAte).HasColumnName("atualizado_ate");
    }
}
