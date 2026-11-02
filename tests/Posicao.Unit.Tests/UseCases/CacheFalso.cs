namespace Posicao.Unit.Tests.UseCases;

internal sealed class CacheFalso : ICacheDeSaldos
{
    public Dictionary<(ContaId, DataFechamento), decimal> Aberturas { get; } = [];

    public bool ForaDoAr { get; set; }

    public int Limpezas { get; private set; }

    public Task<decimal?> ObterAberturaAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento)
    {
        Falhar();
        return Task.FromResult(Aberturas.TryGetValue((contaId, data), out var valor) ? valor : (decimal?)null);
    }

    public Task GuardarAberturaAsync(ContaId contaId, DataFechamento data, decimal saldo, CancellationToken cancelamento)
    {
        Falhar();
        Aberturas[(contaId, data)] = saldo;
        return Task.CompletedTask;
    }

    public Task LimparAsync(CancellationToken cancelamento)
    {
        Falhar();
        Aberturas.Clear();
        Limpezas++;
        return Task.CompletedTask;
    }

    private void Falhar()
    {
        if (ForaDoAr)
        {
            throw new InvalidOperationException("Cache fora do ar.");
        }
    }
}
