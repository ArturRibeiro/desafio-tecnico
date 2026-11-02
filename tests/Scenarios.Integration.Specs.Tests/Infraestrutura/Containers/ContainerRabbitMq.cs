namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Containers;

/// <summary>
/// RabbitMQ da execução. Cada cenário cria e apaga o seu virtual host nele. Usa uma porta fixa no host para manter o
/// endereço depois de parado e iniciado de novo, nos cenários com o broker fora do ar.
/// </summary>
public static class ContainerRabbitMq
{
    private const string Usuario = "teste";

    private static readonly int Porta = PortaLivre();

    private static readonly RabbitMqContainer Container = new RabbitMqBuilder("rabbitmq:3.13-alpine")
        .WithUsername(Usuario)
        .WithPassword(Usuario)
        .WithPortBinding(Porta, 5672)
        .Build();

    public static string Uri(string virtualHost) =>
        $"amqp://{Usuario}:{Usuario}@127.0.0.1:{Porta}/{System.Uri.EscapeDataString(virtualHost)}";

    public static Task IniciarAsync() => Container.StartAsync();

    public static async Task EncerrarAsync() => await Container.DisposeAsync();

    public static Task PararAsync() => Container.StopAsync();

    public static Task SubirAsync() => Container.StartAsync();

    public static async Task CriarVirtualHostAsync(string nome)
    {
        await RabbitMqCtlAsync("add_vhost", nome);
        await RabbitMqCtlAsync("set_permissions", "-p", nome, Usuario, ".*", ".*", ".*");
    }

    public static async Task ApagarVirtualHostAsync(string nome) => await RabbitMqCtlAsync("delete_vhost", nome);

    private static async Task RabbitMqCtlAsync(params string[] argumentos)
    {
        var resultado = await Container.ExecAsync(["rabbitmqctl", .. argumentos]);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException($"rabbitmqctl {string.Join(' ', argumentos)} falhou: {resultado.Stderr}");
        }
    }

    private static int PortaLivre()
    {
        using var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        return ((IPEndPoint)ouvinte.LocalEndpoint).Port;
    }
}
