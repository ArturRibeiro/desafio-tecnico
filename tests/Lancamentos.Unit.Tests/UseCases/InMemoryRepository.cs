namespace Lancamentos.Unit.Tests.UseCases;

/// <summary>Portas em memória. A unidade de trabalho só grava o que foi adicionado quando o resultado é sucesso.</summary>
internal sealed class InMemoryRepository : IUnitOfWork, IContaRepository, ILancamentoRepository
{
    private readonly List<Conta> contasPendentes = [];
    private readonly List<Lancamento> lancamentosPendentes = [];

    public List<Conta> Contas { get; } = [];

    public List<Lancamento> Lancamentos { get; } = [];

    public List<string> Chamadas { get; } = [];

    public async Task<Resultado<T>> ExecutarAsync<T>(Func<CancellationToken, Task<Resultado<T>>> operacao, CancellationToken cancelamento)
    {
        var resultado = await operacao(cancelamento);
        if (resultado.Sucesso)
        {
            Contas.AddRange(contasPendentes);
            Lancamentos.AddRange(lancamentosPendentes);
        }

        contasPendentes.Clear();
        lancamentosPendentes.Clear();
        return resultado;
    }

    public Task<Conta?> ObterParaAlteracaoAsync(ContaId id, CancellationToken cancelamento)
    {
        Chamadas.Add("conta");
        return Task.FromResult(Contas.SingleOrDefault(c => c.Id == id));
    }

    public Task<Conta?> ObterPorChaveDeAberturaAsync(TitularId titular, ChaveIdempotencia chave, CancellationToken cancelamento) =>
        Task.FromResult(Contas.SingleOrDefault(c => c.Titular == titular && c.ChaveAbertura == chave));

    public void Adicionar(Conta conta) => contasPendentes.Add(conta);

    public Task<Lancamento?> ObterAsync(ContaId contaId, LancamentoId id, CancellationToken cancelamento) =>
        Task.FromResult(Lancamentos.SingleOrDefault(l => l.ContaId == contaId && l.Id == id));

    public Task<Lancamento?> ObterPorChaveAsync(ContaId contaId, ChaveIdempotencia chave, CancellationToken cancelamento)
    {
        Chamadas.Add("chave");
        return Task.FromResult(Lancamentos.SingleOrDefault(l => l.ContaId == contaId && l.ChaveIdempotencia == chave));
    }

    public Task<bool> ExisteEstornoDeAsync(LancamentoId id, CancellationToken cancelamento) =>
        Task.FromResult(Lancamentos.Any(l => l.LancamentoEstornadoId == id));

    public void Adicionar(Lancamento lancamento) => lancamentosPendentes.Add(lancamento);
}
