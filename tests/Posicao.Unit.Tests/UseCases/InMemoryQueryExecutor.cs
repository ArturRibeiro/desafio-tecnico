namespace Posicao.Unit.Tests.UseCases;

/// <summary>
/// Executa em memória, com as extensões assíncronas do EF Core (FirstOrDefaultAsync, SumAsync), as consultas do
/// <see cref="InMemoryDbContextReadOnly"/>. Antes, troca o cast <c>(decimal)(object)</c> de um <see cref="Valor"/>
/// pela sua <see cref="Valor.Quantia"/>: no EF, esse cast lê a coluna por trás do Value Object; em memória, seria um
/// unboxing inválido.
/// </summary>
internal sealed class InMemoryQueryExecutor : ExpressionVisitor, IAsyncQueryProvider
{
    public static readonly InMemoryQueryExecutor Instancia = new();

    private static readonly MethodInfo ExecutarGenerico =
        typeof(InMemoryQueryExecutor).GetMethod(nameof(Execute), 1, [typeof(Expression)])!;

    private static readonly MethodInfo Concluida = typeof(Task).GetMethod(nameof(Task.FromResult))!;

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new InMemoryQuery<TElement>(this, expression);

    public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

    public object? Execute(Expression expression) => throw new NotSupportedException();

    public TResult Execute<TResult>(Expression expression)
    {
        var emMemoria = Visit(expression);
        return typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? (TResult)Activator.CreateInstance(typeof(EnumerableQuery<>).MakeGenericType(typeof(TResult).GetGenericArguments()), emMemoria)!
            : ((IQueryProvider)new EnumerableQuery<TResult>(Array.Empty<TResult>())).Execute<TResult>(emMemoria);
    }

    /// <summary>O EF pede um <c>Task&lt;X&gt;</c>: executa a consulta e devolve o resultado já concluído.</summary>
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var tipo = typeof(TResult).GetGenericArguments()[0];
        var resultado = ExecutarGenerico.MakeGenericMethod(tipo).Invoke(this, [expression]);
        return (TResult)Concluida.MakeGenericMethod(tipo).Invoke(null, [resultado])!;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node is { NodeType: ExpressionType.Convert, Operand: UnaryExpression { NodeType: ExpressionType.Convert, Operand: var valor } }
            && node.Type == typeof(decimal) && valor.Type == typeof(Valor)
            ? Expression.Property(Visit(valor), nameof(Valor.Quantia))
            : base.VisitUnary(node);
    }
}
