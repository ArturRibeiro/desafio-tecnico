namespace Posicao.Infrastructure.Persistencia;

/// <summary>
/// Uma transação por evento, com <c>pg_advisory_xact_lock</c> sobre a conta: os eventos da mesma conta correm em
/// série, inclusive antes de a titularidade existir (não há linha para <c>SELECT ... FOR UPDATE</c>). O lock é liberado
/// no fim da transação. Contas diferentes não disputam o lock.
/// </summary>
internal sealed class UnidadeDeTrabalho(PosicaoDbContext banco) : IUnidadeDeTrabalho
{
    public async Task<T> ExecutarNaContaAsync<T>(ContaId contaId, Func<CancellationToken, Task<T>> operacao, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        // Dentro de um consumer, o Inbox do MassTransit já abriu a transação e a confirma depois do consumer.
        if (banco.Database.CurrentTransaction is not null)
        {
            return await ExecutarComLockAsync(contaId, operacao, cancelamento).ConfigureAwait(false);
        }

        await using var transacao = await banco.Database.BeginTransactionAsync(cancelamento).ConfigureAwait(false);
        var resultado = await ExecutarComLockAsync(contaId, operacao, cancelamento).ConfigureAwait(false);
        await transacao.CommitAsync(cancelamento).ConfigureAwait(false);
        return resultado;
    }

    private async Task<T> ExecutarComLockAsync<T>(ContaId contaId, Func<CancellationToken, Task<T>> operacao, CancellationToken cancelamento)
    {
        await banco.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({contaId.Valor.ToString()}, 0))", cancelamento)
            .ConfigureAwait(false);
        var resultado = await operacao(cancelamento).ConfigureAwait(false);
        await banco.SaveChangesAsync(cancelamento).ConfigureAwait(false);
        return resultado;
    }
}
