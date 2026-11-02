namespace Plataforma.Mediator.Imp;

/// <summary>Resolve o handler de <typeparamref name="TRequest"/> no escopo e entrega o pedido a ele.</summary>
internal sealed class RequestDispatcher<TRequest, TResponse> : RequestDispatcher<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> SendAsync(IRequest<TResponse> request, IServiceProvider servicos, CancellationToken cancelamento)
    {
        var handler = servicos.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>
            ?? throw new InvalidOperationException($"Nenhum handler registrado para {typeof(TRequest).Name}.");
        return handler.HandleAsync((TRequest)request, cancelamento);
    }
}
