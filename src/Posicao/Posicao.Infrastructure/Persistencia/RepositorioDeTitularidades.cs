namespace Posicao.Infrastructure.Persistencia;

internal sealed class RepositorioDeTitularidades(PosicaoDbContext banco) : IRepositorioDeTitularidades
{
    public Task<Titularidade?> ObterAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.Titularidades.SingleOrDefaultAsync(t => t.ContaId == contaId, cancelamento);

    public async Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento) =>
        await banco.Titularidades.AsNoTracking().Select(t => t.ContaId).ToListAsync(cancelamento).ConfigureAwait(false);

    public void Adicionar(Titularidade titularidade) => banco.Titularidades.Add(titularidade);
}
