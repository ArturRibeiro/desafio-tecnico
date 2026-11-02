namespace Posicao.Application.Portas;

public interface IRepositorioDeMovimentos
{
    Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento);

    /// <summary>Movimentos gravados com sequência maior que <paramref name="aposSequencia"/>, em ordem.</summary>
    Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento);

    Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento);

    /// <summary>
    /// Soma dos efeitos (crédito positivo, débito negativo) dos movimentos aplicados até <paramref name="ateSequencia"/>,
    /// com competência entre <paramref name="de"/> e <paramref name="ate"/>, inclusive.
    /// </summary>
    Task<decimal> SomarAplicadosAsync(ContaId contaId, DateTimeOffset de, DateTimeOffset ate, long ateSequencia, CancellationToken cancelamento);

    void Adicionar(Movimento movimento);
}
