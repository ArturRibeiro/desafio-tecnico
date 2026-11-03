namespace Posicao.Unit.Tests.UseCases;

/// <summary>Consulta em memória que também é <see cref="IAsyncEnumerable{T}"/>, como as do EF Core, para o ToListAsync.</summary>
internal sealed class InMemoryQuery<T>(InMemoryQueryExecutor executor, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);

    public Expression Expression { get; } = expression;

    public IQueryProvider Provider => executor;

    public IEnumerator<T> GetEnumerator() => executor.Execute<IEnumerable<T>>(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in this)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
