namespace Posicao.Application.Portas;

/// <summary>
/// Cache opcional dos saldos de abertura definitivos (DOMINIO.md, seção 6.3). Pode lançar exceção quando o cache
/// está fora do ar; os casos de uso a ignoram e seguem pelo banco.
/// </summary>
public interface ICacheDeSaldos
{
    Task<decimal?> ObterAberturaAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento);

    Task GuardarAberturaAsync(ContaId contaId, DataFechamento data, decimal saldo, CancellationToken cancelamento);

    Task LimparAsync(CancellationToken cancelamento);
}
