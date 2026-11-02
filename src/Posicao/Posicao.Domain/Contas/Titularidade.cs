namespace Posicao.Domain.Contas;

/// <summary>
/// Aggregate Root: quem é o titular da conta (DOMINIO.md, seção 6.2), criada a partir de <c>ContaAberta v1</c>.
/// Guarda também o estado da projeção da conta (última sequência aplicada e até quando os dados estão atualizados),
/// porque ele existe antes do primeiro fechamento.
/// </summary>
public sealed class Titularidade
{
    private Titularidade()
    {
        // Usado pelo EF Core.
    }

    public ContaId ContaId { get; private set; }

    public TitularId Titular { get; private set; }

    public DateTimeOffset DataAbertura { get; private set; }

    /// <summary>Última sequência aplicada aos fechamentos. A abertura é a sequência 0.</summary>
    public long UltimaSequenciaAplicada { get; private set; }

    /// <summary>Instante de registro da última movimentação aplicada; antes da primeira, o da abertura.</summary>
    public DateTimeOffset AtualizadoAte { get; private set; }

    public static Titularidade Registrar(ContaId contaId, TitularId titular, DateTimeOffset dataAbertura)
    {
        var abertura = Instantes.Normalizar(dataAbertura);
        return new Titularidade
        {
            ContaId = contaId,
            Titular = titular,
            DataAbertura = abertura,
            UltimaSequenciaAplicada = 0,
            AtualizadoAte = abertura,
        };
    }

    public bool PertenceA(TitularId titular) => Titular == titular;

    internal void Avancar(Movimento movimento)
    {
        UltimaSequenciaAplicada = movimento.Sequencia;
        AtualizadoAte = movimento.DataRegistro;
    }

    /// <summary>Volta ao estado da abertura, para a reconstrução reaplicar todos os movimentos.</summary>
    public void Reiniciar()
    {
        UltimaSequenciaAplicada = 0;
        AtualizadoAte = DataAbertura;
    }
}
