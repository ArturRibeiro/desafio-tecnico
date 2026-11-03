namespace Posicao.Worker;

/// <summary>Grava e aplica, em ordem de sequência, o movimento de <see cref="MovimentacaoRegistradaV1"/>.</summary>
internal sealed class MovimentacaoRegistradaConsumer(IMediator mediator, ILogger<MovimentacaoRegistradaConsumer> log)
    : IConsumer<MovimentacaoRegistradaV1>
{
    public async Task Consume(ConsumeContext<MovimentacaoRegistradaV1> context)
    {
        using var escopo = Observabilidade.MarcarCorrelacao(log, context.Message.CorrelacaoId);
        var movimento = EventoTranslator.Traduzir(context.Message);
        var resumo = await mediator.SendAsync(new ProjetarMovimentacaoCommand(movimento), context.CancellationToken).ConfigureAwait(false);
        Registros.Projetado(log, "MovimentacaoRegistrada", context.Message.EventoId, context.Message.CorrelacaoId, resumo);
    }
}
