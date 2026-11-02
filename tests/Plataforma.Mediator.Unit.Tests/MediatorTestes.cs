namespace Plataforma.Mediator.Unit.Tests;

/// <summary>O Mediator entrega cada pedido ao handler registrado para o tipo dele.</summary>
public sealed class MediatorTestes
{
    [Fact(DisplayName = "Mediator entrega o pedido ao handler do tipo dele e devolve a resposta")]
    public async Task SendAsync_EntregaAoHandlerDoTipoDoPedidoEDevolveARespostaDele()
    {
        var mediator = new ServiceProviderMediator(new Provedor(new DobrarHandler()));

        (await mediator.SendAsync(new DobrarQuery(21), Ct)).Should().Be(42);
    }

    [Fact(DisplayName = "Mediator sem handler registrado falha informando o nome do pedido")]
    public async Task SendAsync_SemHandlerRegistrado_FalhaComONomeDoPedido()
    {
        var mediator = new ServiceProviderMediator(new Provedor(null));

        var erro = (await FluentActions.Awaiting(() => mediator.SendAsync(new DobrarQuery(1), Ct)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;
        erro.Message.Should().Contain(nameof(DobrarQuery));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record DobrarQuery(int Valor) : IRequest<int>;

    private sealed class DobrarHandler : IRequestHandler<DobrarQuery, int>
    {
        public Task<int> HandleAsync(DobrarQuery request, CancellationToken cancelamento) => Task.FromResult(request.Valor * 2);
    }

    private sealed class Provedor(object? handler) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRequestHandler<DobrarQuery, int>) ? handler : null;
    }
}
