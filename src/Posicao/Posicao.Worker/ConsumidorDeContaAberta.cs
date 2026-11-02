namespace Posicao.Worker;

/// <summary>Projeta a titularidade da conta a partir de <see cref="ContaAbertaV1"/>.</summary>
internal sealed class ConsumidorDeContaAberta(IMediator mediator, ILogger<ConsumidorDeContaAberta> log) : IConsumer<ContaAbertaV1>
{
    public async Task Consume(ConsumeContext<ContaAbertaV1> context)
    {
        using var escopo = Observabilidade.MarcarCorrelacao(log, context.Message.CorrelacaoId);
        var comando = TradutorDeEventos.Traduzir(context.Message);
        var resumo = await mediator.SendAsync(comando, context.CancellationToken).ConfigureAwait(false);
        Registros.Projetado(log, "ContaAberta", context.Message.EventoId, context.Message.CorrelacaoId, resumo);
    }
}
