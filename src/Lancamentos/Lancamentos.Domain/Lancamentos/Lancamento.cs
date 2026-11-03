namespace Lancamentos.Domain.Lancamentos;

/// <summary>
/// Aggregate Root: registro imutável de uma movimentação (DOMINIO.md, seção 5.2). Só a <see cref="Conta"/> cria
/// lançamentos, na mesma transação que atualiza o saldo; não há métodos de alteração, e a única forma de corrigir um
/// lançamento é estorná-lo.
/// </summary>
public sealed class Lancamento : IAggregateRoot
{
    private Lancamento()
    {
        // Usado pelo EF Core.
    }

    private Lancamento(
        LancamentoId id,
        ContaId contaId,
        TipoLancamento tipo,
        Valor valor,
        Descricao descricao,
        DateTimeOffset dataCompetencia,
        DateTimeOffset dataRegistro,
        long sequencia,
        LancamentoId? lancamentoEstornadoId,
        ChaveIdempotencia chaveIdempotencia,
        decimal saldoDisponivelApos)
    {
        Id = id;
        ContaId = contaId;
        Tipo = tipo;
        Valor = valor;
        Descricao = descricao;
        DataCompetencia = dataCompetencia;
        DataRegistro = dataRegistro;
        Sequencia = sequencia;
        LancamentoEstornadoId = lancamentoEstornadoId;
        ChaveIdempotencia = chaveIdempotencia;
        SaldoDisponivelApos = saldoDisponivelApos;
    }

    public LancamentoId Id { get; private set; }

    public ContaId ContaId { get; private set; }

    public TipoLancamento Tipo { get; private set; }

    public Valor Valor { get; private set; }

    public Descricao Descricao { get; private set; }

    /// <summary>Momento em que a movimentação aconteceu; é o instante usado no cálculo da posição.</summary>
    public DateTimeOffset DataCompetencia { get; private set; }

    /// <summary>Momento em que o sistema gravou o lançamento; usado na auditoria.</summary>
    public DateTimeOffset DataRegistro { get; private set; }

    public long Sequencia { get; private set; }

    /// <summary>Lançamento anulado por este, quando <see cref="Tipo"/> é <see cref="TipoLancamento.Estorno"/>.</summary>
    public LancamentoId? LancamentoEstornadoId { get; private set; }

    public ChaveIdempotencia ChaveIdempotencia { get; private set; }

    /// <summary>
    /// Saldo disponível da conta logo após este lançamento. Permite que a repetição idempotente devolva
    /// exatamente a resposta original e serve à auditoria.
    /// </summary>
    public decimal SaldoDisponivelApos { get; private set; }

    internal static Lancamento Criar(
        ContaId contaId,
        TipoLancamento tipo,
        Valor valor,
        Descricao descricao,
        DateTimeOffset dataCompetencia,
        DateTimeOffset dataRegistro,
        long sequencia,
        LancamentoId? lancamentoEstornadoId,
        ChaveIdempotencia chaveIdempotencia,
        decimal saldoDisponivelApos) =>
        new(LancamentoId.Novo(), contaId, tipo, valor, descricao, dataCompetencia, dataRegistro, sequencia, lancamentoEstornadoId, chaveIdempotencia, saldoDisponivelApos);
}
