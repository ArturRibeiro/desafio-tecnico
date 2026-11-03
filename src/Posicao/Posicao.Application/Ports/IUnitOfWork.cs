namespace Posicao.Application.Ports;

/// <summary>
/// Executa uma operação sobre uma conta numa transação, com um lock exclusivo da conta até o fim dela: os eventos
/// da mesma conta são aplicados em série, inclusive antes de a titularidade existir. Grava tudo ao final.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecutarNaContaAsync<T>(ContaId contaId, Func<CancellationToken, Task<T>> operacao, CancellationToken cancelamento);
}
