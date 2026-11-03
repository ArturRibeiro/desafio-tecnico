namespace Lancamentos.Infrastructure.Persistencia;

/// <summary>
/// Uma transação por operação. No sucesso, os eventos de domínio viram mensagens no Outbox e são confirmados
/// no mesmo SaveChanges da conta e do lançamento; na falha, nada é gravado.
/// </summary>
internal sealed class UnitOfWork(LancamentosDbContext banco, EventoPublisher publicador) : IUnitOfWork
{
    public async Task<Resultado<T>> ExecutarAsync<T>(Func<CancellationToken, Task<Resultado<T>>> operacao, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        await using var transacao = await banco.Database.BeginTransactionAsync(cancelamento).ConfigureAwait(false);
        try
        {
            var resultado = await operacao(cancelamento).ConfigureAwait(false);
            if (!resultado.Sucesso)
            {
                await DesfazerAsync(transacao, cancelamento).ConfigureAwait(false);
                return resultado;
            }

            var contas = banco.ChangeTracker.Entries<Conta>().Select(e => e.Entity).ToList();
            await publicador.PublicarAsync(contas.SelectMany(c => c.Eventos), cancelamento).ConfigureAwait(false);
            await banco.SaveChangesAsync(cancelamento).ConfigureAwait(false);
            await transacao.CommitAsync(cancelamento).ConfigureAwait(false);
            contas.ForEach(c => c.LimparEventos());
            return resultado;
        }
        catch (DbUpdateException erro) when (erro.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violacao
            && Traduzir(violacao.ConstraintName) is { } falha)
        {
            await DesfazerAsync(transacao, cancelamento).ConfigureAwait(false);
            return Resultado.Falhar<T>(falha);
        }
    }

    /// <summary>
    /// Índices únicos que reforçam regras de negócio. Com a conta bloqueada eles quase nunca disparam;
    /// quando disparam, a resposta é a mesma regra que o Aggregate aplicaria. A sequência duplicada não é traduzida:
    /// seria um defeito, e deve aparecer como erro.
    /// </summary>
    private static Falha? Traduzir(string? indice) => indice switch
    {
        LancamentosDbContext.Indices.ContaPorTitularEChaveDeAbertura => new Falha(CodigosDeFalhaTecnica.AberturaConcorrente, "Abertura concorrente com a mesma chave."),
        LancamentosDbContext.Indices.EstornoPorLancamento => new Falha(CodigosDeFalha.LancamentoJaEstornado, "O lançamento já foi estornado."),
        LancamentosDbContext.Indices.LancamentoPorContaEChave => new Falha(CodigosDeFalha.ChaveIdempotenciaConflitante, "A Idempotency-Key já foi usada."),
        _ => null,
    };

    private async Task DesfazerAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transacao, CancellationToken cancelamento)
    {
        await transacao.RollbackAsync(cancelamento).ConfigureAwait(false);
        banco.ChangeTracker.Clear();
    }
}
