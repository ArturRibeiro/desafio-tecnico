extern alias PosicaoWorker;

namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>
/// Posicao.Worker no processo, com o banco e o virtual host do cenário, consumindo das filas com o
/// <paramref name="prefixoDasFilas"/> do cenário, o relógio do cenário e a captura das divergências de saldo.
/// </summary>
public sealed class ServicoDoWorker(
    string banco, string virtualHost, string redis, string prefixoDasFilas, TimeProvider relogio, DivergenciasDeSaldo divergencias)
    : WebApplicationFactory<PosicaoWorker::Program>
{
    /// <summary>O mesmo que o comando "reconstruir" do Posicao.Worker faz, pelo Mediator do Worker.</summary>
    public async Task<int> ReconstruirAsync()
    {
        await using var escopo = Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<Plataforma.Mediator.IMediator>()
            .SendAsync(new Posicao.Application.UseCases.ReconstruirPosicao.Imp.ReconstruirPosicaoCommand(), CancellationToken.None);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Postgres", banco);
        builder.UseSetting("ConnectionStrings:RabbitMq", ContainerRabbitMq.Uri(virtualHost));
        builder.UseSetting("ConnectionStrings:Redis", $"{redis},abortConnect=false");
        builder.UseSetting("Banco:AplicarMigracoesNaSubida", "true");
        builder.UseSetting("Banco:IntervaloEntreTentativas", "00:00:00.500");
        builder.UseSetting("Consumo:PrefixoDasFilas", prefixoDasFilas);
        builder.UseSetting("Consumo:IntervaloMinimo", "00:00:00.200");
        builder.UseSetting("Consumo:IntervaloMaximo", "00:00:01");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.ConfigureTestServices(servicos =>
        {
            servicos.AddSingleton(relogio);
            servicos.AddSingleton<ILoggerProvider>(divergencias);
        });
    }
}
