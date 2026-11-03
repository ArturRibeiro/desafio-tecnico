namespace Posicao.Infrastructure.Persistencia.Repositories;

internal sealed class TitularidadeRepository(PosicaoDbContext banco) : ITitularidadeRepository
{
    public Task<Titularidade?> ObterAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.Titularidades.SingleOrDefaultAsync(t => t.ContaId == contaId, cancelamento);

    public async Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento) =>
        await banco.Titularidades.AsNoTracking().Select(t => t.ContaId).ToListAsync(cancelamento).ConfigureAwait(false);

    public void Adicionar(Titularidade titularidade) => banco.Titularidades.Add(titularidade);

    /// <summary>Consulta a função <c>bloquear_conta</c>, que faz <c>pg_advisory_xact_lock</c> sobre a conta (migração ConsultaDeBloqueioDaConta).</summary>
    public Task BloquearContaAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.BloquearConta(contaId.Valor).ToListAsync(cancelamento);
}
