namespace Lancamentos.Application.Portas;

public interface IRepositorioDeContas
{
    /// <summary>
    /// Carrega a conta bloqueando a linha até o fim da transação, para que operações na mesma conta
    /// sejam executadas em série. Só pode ser chamado dentro de <see cref="IUnidadeDeTrabalho.ExecutarAsync{T}"/>.
    /// </summary>
    Task<Conta?> ObterParaAlteracaoAsync(ContaId id, CancellationToken cancelamento);

    Task<Conta?> ObterPorChaveDeAberturaAsync(TitularId titular, ChaveIdempotencia chave, CancellationToken cancelamento);

    void Adicionar(Conta conta);
}
