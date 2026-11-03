namespace Lancamentos.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Mapeamento do Aggregate Root <see cref="Conta"/> na tabela "contas".</summary>
internal sealed class ContaConfiguration : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("contas");
        builder.HasKey(c => c.Id);
        builder.Ignore(c => c.Eventos);

        builder.Property(c => c.Id).HasColumnName("id").HasConversion(ValueConverters.ContaId);
        builder.Property(c => c.Titular).HasColumnName("titular_id").HasConversion(ValueConverters.TitularId).HasMaxLength(TitularId.TamanhoMaximo);
        builder.Property(c => c.ChaveAbertura).HasColumnName("chave_abertura").HasConversion(ValueConverters.Chave).HasMaxLength(ChaveIdempotencia.TamanhoMaximo);
        builder.Property(c => c.SaldoDisponivel).HasColumnName("saldo_disponivel").HasPrecision(14, 2);
        builder.Property(c => c.Limite).HasColumnName("limite").HasPrecision(14, 2);
        builder.Property(c => c.Situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.UltimaSequencia).HasColumnName("ultima_sequencia");
        builder.Property(c => c.Versao).HasColumnName("versao").IsConcurrencyToken();
        builder.Property(c => c.DataAbertura).HasColumnName("data_abertura");

        builder.HasIndex(c => new { c.Titular, c.ChaveAbertura }).IsUnique().HasDatabaseName(LancamentosDbContext.Indices.ContaPorTitularEChaveDeAbertura);
    }
}
