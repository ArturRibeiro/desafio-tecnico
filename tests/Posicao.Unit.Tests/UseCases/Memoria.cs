namespace Posicao.Unit.Tests.UseCases;

/// <summary>Portas em memória: o mesmo comportamento observável dos adaptadores, sem banco nem Redis.</summary>
internal sealed class Memoria : IUnidadeDeTrabalho, IRepositorioDeTitularidades, IRepositorioDeMovimentos, IRepositorioDeFechamentos
{
    public List<Titularidade> Titularidades { get; } = [];

    public List<Movimento> Movimentos { get; } = [];

    public List<FechamentoDiario> Fechamentos { get; } = [];

    public Task<T> ExecutarNaContaAsync<T>(ContaId contaId, Func<CancellationToken, Task<T>> operacao, CancellationToken cancelamento) =>
        operacao(cancelamento);

    Task<Titularidade?> IRepositorioDeTitularidades.ObterAsync(ContaId contaId, CancellationToken cancelamento) =>
        Task.FromResult(Titularidades.SingleOrDefault(t => t.ContaId == contaId));

    public Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<ContaId>>(Titularidades.Select(t => t.ContaId).ToList());

    public void Adicionar(Titularidade titularidade) => Titularidades.Add(titularidade);

    public Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento) =>
        Task.FromResult(Movimentos.Any(m => m.ContaId == contaId && m.Sequencia == sequencia));

    public Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<Movimento>>(Movimentos.Where(m => m.ContaId == contaId && m.Sequencia > aposSequencia).OrderBy(m => m.Sequencia).ToList());

    public Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<Movimento>>(Movimentos.Where(m => m.ContaId == contaId && lancamentos.Contains(m.LancamentoId)).ToList());

    public Task<decimal> SomarAplicadosAsync(ContaId contaId, DateTimeOffset de, DateTimeOffset ate, long ateSequencia, CancellationToken cancelamento) =>
        Task.FromResult(Movimentos
            .Where(m => m.ContaId == contaId && m.Efeito is not null && m.Sequencia <= ateSequencia && m.DataCompetencia >= de && m.DataCompetencia <= ate)
            .Sum(m => m.Sinalizado));

    public void Adicionar(Movimento movimento) => Movimentos.Add(movimento);

    public Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento) =>
        Task.FromResult(Fechamentos.Where(f => f.ContaId == contaId).MaxBy(f => f.Data));

    public Task<FechamentoDiario?> ObterUltimoAntesDeAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento) =>
        Task.FromResult(Fechamentos.Where(f => f.ContaId == contaId && f.Data < data).MaxBy(f => f.Data));

    public Task<IReadOnlyList<FechamentoDiario>> ListarAsync(ContaId contaId, DataFechamento de, DataFechamento ate, int pular, int quantidade, CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<FechamentoDiario>>(Fechamentos
            .Where(f => f.ContaId == contaId && f.Data >= de && f.Data <= ate)
            .OrderBy(f => f.Data)
            .Skip(pular)
            .Take(quantidade)
            .ToList());

    public Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento)
    {
        Fechamentos.RemoveAll(f => f.ContaId == contaId);
        return Task.CompletedTask;
    }

    public void Adicionar(FechamentoDiario fechamento) => Fechamentos.Add(fechamento);
}
