namespace Posicao.Infrastructure.Persistencia.ReadOnly;

/// <summary><see cref="IDbContextReadOnly"/> sobre o <see cref="PosicaoDbContext"/>; o AsNoTracking fica nos handlers de Query.</summary>
internal sealed class DbContextReadOnly(PosicaoDbContext banco) : IDbContextReadOnly
{
    public IQueryable<T> Query<T>()
        where T : class =>
        banco.Set<T>();
}
