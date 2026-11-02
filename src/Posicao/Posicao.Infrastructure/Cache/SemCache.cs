namespace Posicao.Infrastructure.Cache;

/// <summary>Cache desligado (sem connection string do Redis): toda consulta vai ao banco.</summary>
internal sealed class SemCache : ICacheDeSaldos
{
    public Task<decimal?> ObterAberturaAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento) => Task.FromResult<decimal?>(null);

    public Task GuardarAberturaAsync(ContaId contaId, DataFechamento data, decimal saldo, CancellationToken cancelamento) => Task.CompletedTask;

    public Task LimparAsync(CancellationToken cancelamento) => Task.CompletedTask;
}
