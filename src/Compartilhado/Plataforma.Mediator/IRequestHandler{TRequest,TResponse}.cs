namespace Plataforma.Mediator;

/// <summary>Caso de uso: trata um único tipo de pedido.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancelamento);
}
