namespace Posicao.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Resultado da função <c>bloquear_conta</c>: sem chave e sem tabela, só lido.</summary>
internal sealed class BloqueioDaContaConfiguration : IEntityTypeConfiguration<BloqueioDaConta>
{
    public void Configure(EntityTypeBuilder<BloqueioDaConta> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasNoKey().ToTable((string?)null);
        builder.Property(b => b.ContaId).HasColumnName("conta_id");
    }
}
