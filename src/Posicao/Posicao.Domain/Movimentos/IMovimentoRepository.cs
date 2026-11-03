namespace Posicao.Domain.Movimentos;

public interface IMovimentoRepository
{
    Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento);

    /// <summary>Movimentos gravados com sequência maior que <paramref name="aposSequencia"/>, em ordem.</summary>
    Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento);

    Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento);

    void Adicionar(Movimento movimento);
}
