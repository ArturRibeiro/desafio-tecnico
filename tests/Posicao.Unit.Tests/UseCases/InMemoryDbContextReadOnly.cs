namespace Posicao.Unit.Tests.UseCases;

/// <summary><see cref="IDbContextReadOnly"/> sobre as listas do <see cref="InMemoryRepository"/>, lidas no momento da consulta.</summary>
internal sealed class InMemoryDbContextReadOnly(InMemoryRepository memoria) : IDbContextReadOnly
{
    public IQueryable<T> Query<T>()
        where T : class
    {
        var fonte = (IEnumerable<T>?)(memoria.Titularidades as IEnumerable<T>)
            ?? (IEnumerable<T>?)(memoria.Movimentos as IEnumerable<T>)
            ?? memoria.Fechamentos as IEnumerable<T>
            ?? throw new NotSupportedException($"Sem dados em memória para {typeof(T).Name}.");
        return new InMemoryQuery<T>(InMemoryQueryExecutor.Instancia, new EnumerableQuery<T>(fonte).AsQueryable().Expression);
    }
}
