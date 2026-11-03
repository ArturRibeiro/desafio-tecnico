namespace Posicao.Unit.Tests.UseCases;

/// <summary>Portas em memória: o mesmo comportamento observável dos adaptadores, sem banco nem Redis.</summary>
internal sealed class InMemoryRepository : IUnitOfWork, ITitularidadeRepository, IMovimentoRepository, IFechamentoDiarioRepository
{
    public List<Titularidade> Titularidades { get; } = [];

    public List<Movimento> Movimentos { get; } = [];

    public List<FechamentoDiario> Fechamentos { get; } = [];

    public Task<T> ExecutarNaContaAsync<T>(ContaId contaId, Func<CancellationToken, Task<T>> operacao, CancellationToken cancelamento) =>
        operacao(cancelamento);

    Task<Titularidade?> ITitularidadeRepository.ObterAsync(ContaId contaId, CancellationToken cancelamento) =>
        Task.FromResult(Titularidades.SingleOrDefault(t => t.ContaId == contaId));

    public Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<ContaId>>(Titularidades.Select(t => t.ContaId).ToList());

    /// <summary>Em memória, os testes rodam numa única thread: não há o que bloquear.</summary>
    public Task BloquearContaAsync(ContaId contaId, CancellationToken cancelamento) => Task.CompletedTask;

    public void Adicionar(Titularidade titularidade) => Titularidades.Add(titularidade);

    public Task<bool> ExisteAsync(ContaId contaId, long sequencia, CancellationToken cancelamento) =>
        Task.FromResult(Movimentos.Any(m => m.ContaId == contaId && m.Sequencia == sequencia));

    public Task<IReadOnlyList<Movimento>> ObterAPartirDeAsync(ContaId contaId, long aposSequencia, CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<Movimento>>(Movimentos.Where(m => m.ContaId == contaId && m.Sequencia > aposSequencia).OrderBy(m => m.Sequencia).ToList());

    public Task<IReadOnlyList<Movimento>> ObterPorLancamentosAsync(ContaId contaId, IReadOnlyCollection<LancamentoId> lancamentos, CancellationToken cancelamento) =>
        Task.FromResult<IReadOnlyList<Movimento>>(Movimentos.Where(m => m.ContaId == contaId && lancamentos.Contains(m.LancamentoId)).ToList());

    public void Adicionar(Movimento movimento) => Movimentos.Add(movimento);

    public Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento) =>
        Task.FromResult(Fechamentos.Where(f => f.ContaId == contaId).MaxBy(f => f.Data));

    public Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento)
    {
        Fechamentos.RemoveAll(f => f.ContaId == contaId);
        return Task.CompletedTask;
    }

    public void Adicionar(FechamentoDiario fechamento) => Fechamentos.Add(fechamento);
}
