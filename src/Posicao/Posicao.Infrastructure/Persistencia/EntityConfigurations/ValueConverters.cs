namespace Posicao.Infrastructure.Persistencia.EntityConfigurations;

/// <summary>Conversões dos Value Objects. Os valores relidos do banco já foram validados na gravação.</summary>
internal static class ValueConverters
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
