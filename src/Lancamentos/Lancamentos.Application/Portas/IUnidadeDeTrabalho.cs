namespace Lancamentos.Application.Portas;

/// <summary>
/// Executa uma operação numa transação. Se o resultado for sucesso, publica os eventos de domínio pendentes
/// (no Outbox) e confirma tudo junto; se for falha, desfaz e não grava nada.
/// </summary>
public interface IUnidadeDeTrabalho
{
    Task<Resultado<T>> ExecutarAsync<T>(Func<CancellationToken, Task<Resultado<T>>> operacao, CancellationToken cancelamento);
}
