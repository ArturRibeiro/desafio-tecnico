namespace Plataforma.Mediator.Imp;

/// <summary>Despachante de um tipo de pedido, visto só pelo tipo da resposta.</summary>
internal abstract class RequestDispatcher<TResponse>
{
    public abstract Task<TResponse> SendAsync(IRequest<TResponse> request, IServiceProvider servicos, CancellationToken cancelamento);
}
