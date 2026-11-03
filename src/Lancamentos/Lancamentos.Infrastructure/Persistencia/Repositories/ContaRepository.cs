namespace Lancamentos.Infrastructure.Persistencia.Repositories;

internal sealed class ContaRepository(LancamentosDbContext banco) : IContaRepository
{
    /// <summary>
    /// Carrega a conta com a linha bloqueada (<c>FOR UPDATE</c>, pelo <see cref="BloqueioParaAlteracaoInterceptor"/>)
    /// até o fim da transação: as operações na mesma conta correm em série.
    /// </summary>
    public Task<Conta?> ObterParaAlteracaoAsync(ContaId id, CancellationToken cancelamento) =>
        banco.Contas
            .TagWith(BloqueioParaAlteracaoInterceptor.Tag)
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancelamento);

    public Task<Conta?> ObterPorChaveDeAberturaAsync(TitularId titular, ChaveIdempotencia chave, CancellationToken cancelamento) =>
        banco.Contas.SingleOrDefaultAsync(c => c.Titular == titular && c.ChaveAbertura == chave, cancelamento);

    public void Adicionar(Conta conta) => banco.Contas.Add(conta);
}
