namespace Plataforma.Mediator;

/// <summary>Pedido a um caso de uso (comando ou consulta), entregue pelo <see cref="IMediator"/> ao handler dele.</summary>
/// <typeparam name="TResponse">O que o caso de uso devolve.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040", Justification = "Interface marcadora: liga o pedido ao tipo da resposta.")]
public interface IRequest<out TResponse>;
