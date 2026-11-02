namespace Plataforma.Mediator;

/// <summary>
/// Ponto único de entrada da Application de cada Bounded Context: quem chama (Api, Worker) conhece só o pedido, não o handler que o trata.
/// </summary>
public interface IMediator
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancelamento);
}
