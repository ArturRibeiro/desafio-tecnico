namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Cenario;

/// <summary>
/// Containers e configuração de tokens da execução, iniciados uma vez pelo <see cref="Hooks"/>. Cada container tem a
/// sua classe em Containers; cada cenário usa bancos, um virtual host e o Redis limpo (ver <see cref="ScenarioContextInfrastructure"/>).
/// </summary>
public static class Ambiente
{
    public const string Emissor = "business-tests";
    public const string Audiencia = "business-tests";

    /// <summary>Chave HS256 desta execução, usada pelos serviços e pelo emissor de tokens.</summary>
    public static string Chave { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static async Task IniciarAsync()
        => await Task.WhenAll(ContainerPostgres.IniciarAsync()
            , ContainerRabbitMq.IniciarAsync()
            , ContainerRedis.IniciarAsync());

    public static async Task EncerrarAsync()
    {
        await ContainerPostgres.EncerrarAsync();
        await ContainerRabbitMq.EncerrarAsync();
        await ContainerRedis.EncerrarAsync();
    }
}
