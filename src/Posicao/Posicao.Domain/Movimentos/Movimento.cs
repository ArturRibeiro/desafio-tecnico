namespace Posicao.Domain.Movimentos;

/// <summary>
/// Entity: cópia de leitura de um lançamento recebido de Lançamentos (DOMINIO.md, seção 6.2). Fica pendente
/// enquanto a sua sequência for maior que a próxima esperada da conta; o <see cref="Efeito"/> é definido na aplicação.
/// </summary>
public sealed class Movimento
{
    private Movimento()
    {
        // Usado pelo EF Core.
    }

    public ContaId ContaId { get; private set; }

    /// <summary>Ordem na conta, a partir de 1 (a abertura é a 0).</summary>
    public long Sequencia { get; private set; }

    public LancamentoId LancamentoId { get; private set; }

    public TipoMovimento Tipo { get; private set; }

    public Valor Valor { get; private set; }

    public DateTimeOffset DataCompetencia { get; private set; }

    public DateTimeOffset DataRegistro { get; private set; }

    public LancamentoId? LancamentoEstornadoId { get; private set; }

    /// <summary>Nulo enquanto o movimento não foi aplicado.</summary>
    public Efeito? Efeito { get; private set; }

    /// <summary>Quanto o movimento soma ao saldo: positivo para crédito, negativo para débito. Só depois de aplicado.</summary>
    public decimal Sinalizado => Efeito switch
    {
        Movimentos.Efeito.Credito => Valor.Quantia,
        Movimentos.Efeito.Debito => -Valor.Quantia,
        _ => throw new InvalidOperationException("Movimento ainda não aplicado."),
    };

    public static Resultado<Movimento> Criar(
        ContaId contaId,
        long sequencia,
        LancamentoId lancamentoId,
        TipoMovimento tipo,
        Valor valor,
        DateTimeOffset dataCompetencia,
        DateTimeOffset dataRegistro,
        LancamentoId? lancamentoEstornadoId)
    {
        if (sequencia < 1)
        {
            return Resultado.Falhar<Movimento>(CodigosDeFalha.MovimentoInvalido, "A sequência de um movimento começa em 1.");
        }

        if ((tipo == TipoMovimento.Estorno) != lancamentoEstornadoId.HasValue)
        {
            return Resultado.Falhar<Movimento>(CodigosDeFalha.MovimentoInvalido, "Só o estorno, e sempre ele, indica o lançamento estornado.");
        }

        return Resultado.Ok(new Movimento
        {
            ContaId = contaId,
            Sequencia = sequencia,
            LancamentoId = lancamentoId,
            Tipo = tipo,
            Valor = valor,
            DataCompetencia = Instantes.Normalizar(dataCompetencia),
            DataRegistro = Instantes.Normalizar(dataRegistro),
            LancamentoEstornadoId = lancamentoEstornadoId,
        });
    }

    internal void DefinirEfeito(Efeito efeito) => Efeito = efeito;

    /// <summary>Volta a pendente, para a reconstrução reaplicá-lo.</summary>
    internal void Reiniciar() => Efeito = null;
}
