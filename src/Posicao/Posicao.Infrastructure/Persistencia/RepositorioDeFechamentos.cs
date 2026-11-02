namespace Posicao.Infrastructure.Persistencia;

/// <summary>
/// As comparações por data usam SQL: o operador de <see cref="DataFechamento"/> não é traduzido pelo EF.
/// As consultas não são compostas no IQueryable, para o EF não envolvê-las numa subconsulta.
/// </summary>
internal sealed class RepositorioDeFechamentos(PosicaoDbContext banco) : IRepositorioDeFechamentos
{
    public async Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento) =>
        (await banco.Fechamentos
            .FromSql($"SELECT * FROM fechamentos_diarios WHERE conta_id = {contaId.Valor} ORDER BY data DESC LIMIT 1")
            .ToListAsync(cancelamento)
            .ConfigureAwait(false)).SingleOrDefault();

    public async Task<FechamentoDiario?> ObterUltimoAntesDeAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento) =>
        (await banco.Fechamentos
            .FromSql($"SELECT * FROM fechamentos_diarios WHERE conta_id = {contaId.Valor} AND data < {data.Valor} ORDER BY data DESC LIMIT 1")
            .AsNoTracking()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false)).SingleOrDefault();

    public async Task<IReadOnlyList<FechamentoDiario>> ListarAsync(ContaId contaId, DataFechamento de, DataFechamento ate, int pular, int quantidade, CancellationToken cancelamento) =>
        await banco.Fechamentos
            .FromSql($"""
                SELECT * FROM fechamentos_diarios
                WHERE conta_id = {contaId.Valor} AND data >= {de.Valor} AND data <= {ate.Valor}
                ORDER BY data
                OFFSET {pular} LIMIT {quantidade}
                """)
            .AsNoTracking()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento) =>
        banco.Fechamentos.Where(f => f.ContaId == contaId).ExecuteDeleteAsync(cancelamento);

    public void Adicionar(FechamentoDiario fechamento) => banco.Fechamentos.Add(fechamento);
}
