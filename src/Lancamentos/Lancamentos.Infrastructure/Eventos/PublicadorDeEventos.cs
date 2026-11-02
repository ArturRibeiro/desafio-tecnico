namespace Lancamentos.Infrastructure.Eventos;

/// <summary>
/// Publica os Integration Events pelo IPublishEndpoint do escopo. Com o Bus Outbox, a publicação vira uma linha em
/// OutboxMessage gravada no mesmo SaveChanges; o Delivery Service entrega ao RabbitMQ depois.
/// O MessageId é o eventoId: toda reentrega carrega o mesmo identificador.
/// </summary>
internal sealed class PublicadorDeEventos(IPublishEndpoint publicacao, IContextoDaRequisicao contexto)
{
    public async Task PublicarAsync(IEnumerable<IEventoDeDominio> eventos, CancellationToken cancelamento)
    {
        foreach (var evento in eventos)
        {
            var tarefa = evento switch
            {
                ContaAberta aberta => Publicar(TradutorDeEventos.Traduzir(aberta, contexto.CorrelacaoId), aberta.EventoId, cancelamento),
                LancamentoRegistrado registrado => Publicar(TradutorDeEventos.Traduzir(registrado.EventoId, registrado.Lancamento, contexto.CorrelacaoId), registrado.EventoId, cancelamento),
                LancamentoEstornado estornado => Publicar(TradutorDeEventos.Traduzir(estornado.EventoId, estornado.Estorno, contexto.CorrelacaoId), estornado.EventoId, cancelamento),
                _ => throw new InvalidOperationException($"Evento de domínio sem tradução: {evento.GetType().Name}."),
            };
            await tarefa.ConfigureAwait(false);
        }
    }

    private Task Publicar<T>(T mensagem, Guid eventoId, CancellationToken cancelamento)
        where T : class =>
        publicacao.Publish(mensagem, ctx => ctx.MessageId = eventoId, cancelamento);
}
