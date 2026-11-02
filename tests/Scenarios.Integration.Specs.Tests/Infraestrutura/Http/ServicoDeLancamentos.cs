extern alias LancamentosApi;

namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>Lancamentos.Api no processo, com o banco e o virtual host do cenário e o relógio do cenário.</summary>
public sealed class ServicoDeLancamentos(string banco, string virtualHost, TimeProvider relogio) : WebApplicationFactory<LancamentosApi::Program>
{
    public LancamentosDbContext DbContext => Services.CreateScope().ServiceProvider.GetRequiredService<LancamentosDbContext>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Postgres", banco);
        builder.UseSetting("ConnectionStrings:RabbitMq", ContainerRabbitMq.Uri(virtualHost));
        builder.UseSetting("Banco:AplicarMigracoesNaSubida", "true");
        builder.UseSetting("Banco:IntervaloEntreTentativas", "00:00:00.500");
        builder.UseSetting("Autenticacao:Emissor", Ambiente.Emissor);
        builder.UseSetting("Autenticacao:Audiencia", Ambiente.Audiencia);
        builder.UseSetting("Autenticacao:ChaveDeAssinatura", Ambiente.Chave);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.ConfigureTestServices(servicos => servicos.AddSingleton(relogio));
    }
}
