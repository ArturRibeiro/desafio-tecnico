namespace Posicao.Domain.Fechamentos;

/// <summary>
/// Aggregate Root: consolidação dos movimentos de uma conta num dia UTC (DOMINIO.md, seção 6.1).
/// <see cref="SaldoFechamento"/> é sempre abertura + créditos − débitos.
/// </summary>
public sealed class FechamentoDiario
{
    private FechamentoDiario()
    {
        // Usado pelo EF Core.
    }

    public ContaId ContaId { get; private set; }

    public DataFechamento Data { get; private set; }

    public decimal SaldoAbertura { get; private set; }

    public decimal TotalCreditos { get; private set; }

    public decimal TotalDebitos { get; private set; }

    public decimal SaldoFechamento { get; private set; }

    public long UltimaSequenciaAplicada { get; private set; }

    /// <param name="saldoAbertura">Saldo de fechamento do último dia anterior com movimento; zero se não houver.</param>
    public static FechamentoDiario Abrir(ContaId contaId, DataFechamento data, decimal saldoAbertura) =>
        new()
        {
            ContaId = contaId,
            Data = data,
            SaldoAbertura = Dinheiro.DuasCasas(saldoAbertura),
            TotalCreditos = 0.00m,
            TotalDebitos = 0.00m,
            SaldoFechamento = Dinheiro.DuasCasas(saldoAbertura),
            UltimaSequenciaAplicada = 0,
        };

    /// <summary>Soma um movimento já com efeito definido. Uma sequência já aplicada é ignorada (idempotência).</summary>
    public void Aplicar(Movimento movimento)
    {
        ArgumentNullException.ThrowIfNull(movimento);

        if (movimento.ContaId != ContaId || DataFechamento.De(movimento.DataCompetencia) != Data || movimento.Efeito is null)
        {
            throw new InvalidOperationException("Movimento de outra conta, de outro dia ou ainda sem efeito.");
        }

        if (movimento.Sequencia <= UltimaSequenciaAplicada)
        {
            return;
        }

        if (movimento.Efeito == Efeito.Credito)
        {
            TotalCreditos += movimento.Valor.Quantia;
        }
        else
        {
            TotalDebitos += movimento.Valor.Quantia;
        }

        SaldoFechamento = SaldoAbertura + TotalCreditos - TotalDebitos;
        UltimaSequenciaAplicada = movimento.Sequencia;
    }
}
