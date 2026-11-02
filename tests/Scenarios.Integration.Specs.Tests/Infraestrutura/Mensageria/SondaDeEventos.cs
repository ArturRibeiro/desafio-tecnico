namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Mensageria;

/// <summary>
/// Fila durável no virtual host do cenário, ligada às exchanges dos contratos de Lançamentos: guarda tudo o que foi
/// publicado. É durável para continuar recebendo depois de o broker ser parado e iniciado de novo.
/// </summary>
public sealed class SondaDeEventos : IAsyncDisposable
{
    public static readonly string[] Contratos = ["ContaAbertaV1", "MovimentacaoRegistradaV1"];

    private readonly List<EventoRecebido> recebidos = [];
    private readonly Lock trava = new();
    private IConnection? conexao;
    private IChannel? canal;

    public IReadOnlyList<EventoRecebido> Recebidos
    {
        get
        {
            lock (trava)
            {
                return [.. recebidos];
            }
        }
    }

    public static async Task<SondaDeEventos> CriarAsync(string uri, string fila)
    {
        var sonda = new SondaDeEventos();
        var fabrica = new ConnectionFactory { Uri = new Uri(uri), AutomaticRecoveryEnabled = true, NetworkRecoveryInterval = TimeSpan.FromMilliseconds(500) };
        sonda.conexao = await fabrica.CreateConnectionAsync();
        sonda.canal = await sonda.conexao.CreateChannelAsync();
        await sonda.canal.QueueDeclareAsync(fila, durable: true, exclusive: false, autoDelete: false);
        foreach (var contrato in Contratos)
        {
            var exchange = $"Contratos.Integracao.Lancamentos:{contrato}";
            await sonda.canal.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true);
            await sonda.canal.QueueBindAsync(fila, exchange, string.Empty);
        }

        var consumidor = new AsyncEventingBasicConsumer(sonda.canal);
        consumidor.ReceivedAsync += (_, entrega) =>
        {
            using var envelope = JsonDocument.Parse(entrega.Body);
            var tipo = envelope.RootElement.GetProperty("messageType")[0].GetString()!.Split(':')[^1];
            lock (sonda.trava)
            {
                sonda.recebidos.Add(new EventoRecebido(tipo, envelope.RootElement.GetProperty("message").Clone()));
            }

            return Task.CompletedTask;
        };
        await sonda.canal.BasicConsumeAsync(fila, autoAck: true, consumidor);
        return sonda;
    }

    public IReadOnlyList<EventoRecebido> Da(string tipo, string contaId) =>
        Recebidos.Where(e => e.Tipo == tipo && e.Texto("contaId") == contaId).ToList();

    public async ValueTask DisposeAsync()
    {
        if (canal is not null)
        {
            await canal.DisposeAsync();
        }

        if (conexao is not null)
        {
            await conexao.DisposeAsync();
        }
    }
}
