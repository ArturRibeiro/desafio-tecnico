namespace Posicao.Application.Ports;

/// <summary>
/// Leitura dos dados em LINQ, só para as Queries: os handlers de <c>IRequestHandler&lt;...Query&gt;</c> leem por aqui, e
/// não pelos Repositories, que ficam com as escritas. Cada handler aplica AsNoTracking() às suas consultas, que só
/// leem, e as executa pelas extensões assíncronas do EF Core (ToListAsync, FirstOrDefaultAsync, SumAsync).
/// </summary>
public interface IDbContextReadOnly
{
    IQueryable<T> Query<T>()
        where T : class;
}
