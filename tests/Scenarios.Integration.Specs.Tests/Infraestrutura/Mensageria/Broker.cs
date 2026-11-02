namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Mensageria;

/// <summary>Publicação de eventos sintéticos no formato do MassTransit e inspeção de filas, no virtual host do cenário.</summary>
public static class Broker
{
    /// <summary>
    /// Publica um contrato de Lançamentos como o MassTransit faria, para os cenários que precisam de uma ordem, datas ou
    /// conteúdo que Lançamentos nunca produz (eventos fora de ordem, antes da abertura, em dias passados ou inválidos).
    /// </summary>
    public static async Task PublicarAsync(string virtualHost, string contrato, JsonObject mensagem, Guid? messageId = null)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var exchange = $"Contratos.Integracao.Lancamentos:{contrato}";
        var envelope = new JsonObject
        {
            ["messageId"] = (messageId ?? Guid.NewGuid()).ToString(),
            ["conversationId"] = Guid.NewGuid().ToString(),
            ["sourceAddress"] = "rabbitmq://localhost/business-tests",
            ["destinationAddress"] = $"rabbitmq://localhost/{exchange}",
            ["messageType"] = new JsonArray($"urn:message:{exchange}"),
            ["message"] = mensagem.DeepClone(),
            ["sentTime"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };

        await using var conexao = await new ConnectionFactory { Uri = new Uri(ContainerRabbitMq.Uri(virtualHost)) }.CreateConnectionAsync();
        await using var canal = await conexao.CreateChannelAsync();
        await canal.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true);
        var propriedades = new BasicProperties { ContentType = "application/vnd.masstransit+json", DeliveryMode = DeliveryModes.Persistent };
        await canal.BasicPublishAsync(exchange, string.Empty, mandatory: false, propriedades, Encoding.UTF8.GetBytes(envelope.ToJsonString()));
    }

    public static async Task<uint> ContarMensagensAsync(string virtualHost, string fila)
    {
        await using var conexao = await new ConnectionFactory { Uri = new Uri(ContainerRabbitMq.Uri(virtualHost)) }.CreateConnectionAsync();
        await using var canal = await conexao.CreateChannelAsync();
        try
        {
            return (await canal.QueueDeclarePassiveAsync(fila)).MessageCount;
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
        {
            return 0;
        }
    }
}
