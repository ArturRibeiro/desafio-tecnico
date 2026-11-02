namespace Lancamentos.Infrastructure.Persistencia;

internal sealed class RepositorioDeContas(LancamentosDbContext banco) : IRepositorioDeContas
{
    public async Task<Conta?> ObterParaAlteracaoAsync(ContaId id, CancellationToken cancelamento)
    {
        // FOR UPDATE: a linha fica bloqueada até o fim da transação, e as operações na mesma conta correm em série.
        // A consulta não é composta (sem Where/Single no IQueryable) para o EF não envolvê-la numa subconsulta.
        var contas = await banco.Contas
            .FromSql($"SELECT * FROM contas WHERE id = {id.Valor} FOR UPDATE")
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
        return contas.SingleOrDefault();
    }

    public Task<Conta?> ObterPorChaveDeAberturaAsync(TitularId titular, ChaveIdempotencia chave, CancellationToken cancelamento) =>
        banco.Contas.SingleOrDefaultAsync(c => c.Titular == titular && c.ChaveAbertura == chave, cancelamento);

    public void Adicionar(Conta conta) => banco.Contas.Add(conta);
}
