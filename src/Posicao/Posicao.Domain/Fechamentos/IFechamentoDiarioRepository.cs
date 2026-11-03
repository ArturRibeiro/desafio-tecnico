namespace Posicao.Domain.Fechamentos;

public interface IFechamentoDiarioRepository
{
    Task<FechamentoDiario?> ObterUltimoAsync(ContaId contaId, CancellationToken cancelamento);

    Task RemoverTodosAsync(ContaId contaId, CancellationToken cancelamento);

    void Adicionar(FechamentoDiario fechamento);
}
