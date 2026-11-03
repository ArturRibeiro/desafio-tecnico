namespace Posicao.Domain.Contas;

public interface ITitularidadeRepository
{
    Task<Titularidade?> ObterAsync(ContaId contaId, CancellationToken cancelamento);

    Task<IReadOnlyList<ContaId>> ListarContasAsync(CancellationToken cancelamento);

    void Adicionar(Titularidade titularidade);

    /// <summary>
    /// Bloqueia a conta até o fim da transação, para os eventos da mesma conta correrem em série. Funciona antes de a
    /// titularidade existir: um movimento pode chegar antes da abertura, e ainda não há linha da conta para bloquear.
    /// </summary>
    Task BloquearContaAsync(ContaId contaId, CancellationToken cancelamento);
}
