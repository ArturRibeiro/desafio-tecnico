namespace Posicao.Application.Portas;

public interface IRepositorioDeFechamentos
{
    Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento);

    Task<FechamentoDiario?> ObterUltimoAntesDeAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento);

    Task<IReadOnlyList<FechamentoDiario>> ListarAsync(ContaId contaId, DataFechamento de, DataFechamento ate, int pular, int quantidade, CancellationToken cancelamento);

    Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento);

    void Adicionar(FechamentoDiario fechamento);
}
