namespace Posicao.Infrastructure.Persistencia;

/// <summary>
/// Uma transação por evento, com a conta bloqueada pelo <see cref="ITitularidadeRepository.BloquearContaAsync"/>: os
/// eventos da mesma conta correm em série, inclusive antes de a titularidade existir. O bloqueio é liberado no fim da
/// transação. Contas diferentes não disputam o bloqueio.
/// </summary>
internal sealed class UnitOfWork(PosicaoDbContext banco, ITitularidadeRepository titularidades) : IUnitOfWork
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
        await titularidades.BloquearContaAsync(contaId, cancelamento).ConfigureAwait(false);
        var resultado = await operacao(cancelamento).ConfigureAwait(false);
        await banco.SaveChangesAsync(cancelamento).ConfigureAwait(false);
        return resultado;
    }
}
