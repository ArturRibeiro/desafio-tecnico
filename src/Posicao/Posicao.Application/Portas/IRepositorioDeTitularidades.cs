namespace Posicao.Application.Portas;

public interface IRepositorioDeTitularidades
{
    Task<Titularidade?> ObterAsync(ContaId contaId, CancellationToken cancelamento);

    Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento);

    void Adicionar(Titularidade titularidade);
}
