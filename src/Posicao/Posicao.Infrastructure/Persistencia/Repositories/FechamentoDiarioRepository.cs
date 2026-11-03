namespace Posicao.Infrastructure.Persistencia.Repositories;

internal sealed class FechamentoDiarioRepository(PosicaoDbContext banco) : IFechamentoDiarioRepository
{
    public Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.Fechamentos
            .Where(f => f.ContaId == contaId)
            .OrderByDescending(f => f.Data)
            .FirstOrDefaultAsync(cancelamento);

    public Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.Fechamentos.Where(f => f.ContaId == contaId).ExecuteDeleteAsync(cancelamento);

    public void Adicionar(FechamentoDiario fechamento) => banco.Fechamentos.Add(fechamento);
}
