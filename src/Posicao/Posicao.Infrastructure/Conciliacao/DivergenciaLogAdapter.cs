namespace Posicao.Infrastructure.Conciliacao;

/// <summary>
/// Divergência de saldo como log de erro com código estável, pronto para uma regra de alerta, e como contador
/// <c>posicao.conciliacao.divergencias</c>. Só identificadores, nunca valores (spec observabilidade).
/// </summary>
internal sealed partial class DivergenciaLogAdapter : IRegistroDeDivergencias
{
    public const string NomeDoMedidor = "Posicao.Conciliacao";

    private readonly ILogger<DivergenciaLogAdapter> log;
    private readonly Counter<long> divergencias;

    public DivergenciaLogAdapter(ILogger<DivergenciaLogAdapter> log, IMeterFactory medidores)
    {
        ArgumentNullException.ThrowIfNull(medidores);

        this.log = log;
        divergencias = medidores.Create(NomeDoMedidor).CreateCounter<long>(
            "posicao.conciliacao.divergencias",
            description: "Movimentos cujo saldo calculado difere do saldo informado por Lançamentos.");
    }

    public void Registrar(Divergencia divergencia)
    {
        ArgumentNullException.ThrowIfNull(divergencia);

        divergencias.Add(1);
        SaldoDivergente(CodigosDeFalha.SaldoDivergente, divergencia.ContaId.Valor, divergencia.Sequencia, divergencia.LancamentoId.Valor);
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Conciliação ({Codigo}): o saldo calculado da conta {ContaId} após a sequência {Sequencia} (lançamento {LancamentoId}) difere do saldo informado por Lançamentos.")]
    private partial void SaldoDivergente(string codigo, Guid contaId, long sequencia, Guid lancamentoId);
}
