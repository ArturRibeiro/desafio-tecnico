namespace Posicao.Infrastructure.Persistencia.Repositories;

internal sealed class MovimentoRepository(PosicaoDbContext banco) : IMovimentoRepository
{
    public Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento) =>
        banco.Movimentos.AnyAsync(m => m.ContaId == contaId && m.Sequencia == sequencia, cancelamento);

    public async Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento) =>
        await banco.Movimentos
            .Where(m => m.ContaId == contaId && m.Sequencia > aposSequencia)
            .OrderBy(m => m.Sequencia)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento) =>
        await banco.Movimentos
            .Where(m => m.ContaId == contaId && lancamentos.Contains(m.LancamentoId))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public void Adicionar(Movimento movimento) => banco.Movimentos.Add(movimento);
}
