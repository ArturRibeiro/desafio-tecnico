namespace Lancamentos.Domain.Lancamentos;

public interface ILancamentoRepository
{
    Task<Lancamento?> ObterAsync(ContaId contaId, LancamentoId id, CancellationToken cancelamento);

    Task<Lancamento?> ObterPorChaveAsync(ContaId contaId, ChaveIdempotencia chave, CancellationToken cancelamento);

    Task<bool> ExisteEstornoDeAsync(LancamentoId id, CancellationToken cancelamento);

    void Adicionar(Lancamento lancamento);
}
