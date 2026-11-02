extern alias PosicaoApi;

namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>Posicao.Api no processo, com o banco do cenário, o cache de saldos no <paramref name="redis"/> e o relógio do cenário.</summary>
public sealed class ServicoDePosicao(string banco, string redis, TimeProvider relogio) : WebApplicationFactory<PosicaoApi::Program>
{
    public PosicaoDbContext DbContext => Services.CreateScope().ServiceProvider.GetRequiredService<PosicaoDbContext>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Postgres", banco);
        builder.UseSetting("ConnectionStrings:Redis", $"{redis},abortConnect=false");
        builder.UseSetting("Banco:AplicarMigracoesNaSubida", "true");
        builder.UseSetting("Banco:IntervaloEntreTentativas", "00:00:00.500");
        builder.UseSetting("Autenticacao:Emissor", Ambiente.Emissor);
        builder.UseSetting("Autenticacao:Audiencia", Ambiente.Audiencia);
        builder.UseSetting("Autenticacao:ChaveDeAssinatura", Ambiente.Chave);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.ConfigureTestServices(servicos => servicos.AddSingleton(relogio));
    }
}
