namespace Plataforma.Mediator.Imp;

/// <summary>
/// Mediator sobre o contêiner de injeção de dependência: cada pedido vai para o único
/// <see cref="IRequestHandler{TRequest,TResponse}"/> registrado para o tipo dele, no escopo atual.
/// </summary>
public sealed class ServiceProviderMediator(IServiceProvider servicos) : IMediator
{
    private static readonly ConcurrentDictionary<Type, object> Despachantes = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(request);

        // O tipo concreto do pedido só é conhecido em tempo de execução; o despachante tipado é criado uma vez por tipo.
        var despachante = (RequestDispatcher<TResponse>)Despachantes.GetOrAdd(
            request.GetType(),
            static tipo => Activator.CreateInstance(typeof(RequestDispatcher<,>).MakeGenericType(tipo, typeof(TResponse)))!);
        return despachante.SendAsync(request, servicos, cancelamento);
    }
}
