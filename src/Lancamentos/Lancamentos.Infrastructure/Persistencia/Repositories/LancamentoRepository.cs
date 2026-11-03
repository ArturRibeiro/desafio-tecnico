namespace Lancamentos.Infrastructure.Persistencia.Repositories;

internal sealed class LancamentoRepository(LancamentosDbContext banco) : ILancamentoRepository
{
    public Task<Lancamento?> ObterAsync(ContaId contaId, LancamentoId id, CancellationToken cancelamento) =>
        banco.Lancamentos.SingleOrDefaultAsync(l => l.ContaId == contaId && l.Id == id, cancelamento);

    public Task<Lancamento?> ObterPorChaveAsync(ContaId contaId, ChaveIdempotencia chave, CancellationToken cancelamento) =>
        banco.Lancamentos.SingleOrDefaultAsync(l => l.ContaId == contaId && l.ChaveIdempotencia == chave, cancelamento);

    public Task<bool> ExisteEstornoDeAsync(LancamentoId id, CancellationToken cancelamento) =>
        banco.Lancamentos.AnyAsync(l => l.LancamentoEstornadoId == id, cancelamento);

    public void Adicionar(Lancamento lancamento) => banco.Lancamentos.Add(lancamento);
}
