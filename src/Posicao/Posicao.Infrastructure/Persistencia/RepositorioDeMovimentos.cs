namespace Posicao.Infrastructure.Persistencia;

internal sealed class RepositorioDeMovimentos(PosicaoDbContext banco) : IRepositorioDeMovimentos
{
    public Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento) =>
        banco.Movimentos.AnyAsync(m => m.ContaId == contaId && m.Sequencia == sequencia, cancelamento);

    public async Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento) =>
        await banco.Movimentos
            .Where(m => m.ContaId == contaId && m.Sequencia > aposSequencia)
            .OrderBy(m => m.Sequencia)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento) =>
        await banco.Movimentos
            .Where(m => m.ContaId == contaId && lancamentos.Contains(m.LancamentoId))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public Task<decimal> SomarAplicadosAsync(ContaId contaId, DateTimeOffset de, DateTimeOffset ate, long ateSequencia, CancellationToken cancelamento) =>
        banco.Database.SqlQuery<decimal>($"""
            SELECT COALESCE(SUM(CASE efeito WHEN 'Credito' THEN valor ELSE -valor END), 0) AS "Value"
            FROM movimentos
            WHERE conta_id = {contaId.Valor}
              AND efeito IS NOT NULL
              AND sequencia <= {ateSequencia}
              AND data_competencia >= {de.ToUniversalTime()}
              AND data_competencia <= {ate.ToUniversalTime()}
            """).SingleAsync(cancelamento);

    public void Adicionar(Movimento movimento) => banco.Movimentos.Add(movimento);
}
