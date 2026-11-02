namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Containers;

/// <summary>Redis da execução. Cada cenário começa com ele limpo.</summary>
public static class ContainerRedis
{
    private static readonly RedisContainer Container = new RedisBuilder("redis:7-alpine").Build();

    public static string ConnectionString => Container.GetConnectionString();

    public static Task IniciarAsync() => Container.StartAsync();

    public static async Task EncerrarAsync() => await Container.DisposeAsync();

    public static async Task LimparAsync()
    {
        var resultado = await Container.ExecAsync(["redis-cli", "FLUSHALL"]);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException($"redis-cli FLUSHALL falhou: {resultado.Stderr}");
        }
    }
}
