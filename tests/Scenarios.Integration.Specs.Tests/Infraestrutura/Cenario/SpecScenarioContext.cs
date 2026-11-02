namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Cenario;

/// <summary>
/// Estado de um cenário, compartilhado pelos passos por injeção de contexto: o titular, a memória entre os passos e a
/// última resposta. A infraestrutura (bancos, virtual host, serviços) fica em <see cref="ScenarioContextInfrastructure"/>;
/// os membros dela usados pelos passos são repassados daqui.
/// </summary>
public sealed class SpecScenarioContext(ScenarioContextInfrastructure infraestrutura)
{
    /// <summary>DbContext da Lancamentos.Api do cenário.</summary>
    public LancamentosDbContext LancamentosDbContext => infraestrutura.LancamentosDbContext;

    /// <summary>DbContext da Posicao.Api do cenário.</summary>
    public PosicaoDbContext PosicaoDbContext => infraestrutura.PosicaoDbContext;

    /// <summary>Relógio de todos os serviços do cenário.</summary>
    public FakeTimeProvider Relogio => infraestrutura.Relogio;

    /// <summary>Titular padrão do cenário (o "cliente autenticado").</summary>
    public string Titular { get; } = $"titular-{Guid.NewGuid():N}";

    /// <summary>Valores guardados por um passo para os seguintes (contas, lançamentos, respostas anteriores).</summary>
    public Dictionary<string, object> Memoria { get; } = new(StringComparer.Ordinal);

    public Resposta? Ultima { get; private set; }

    /// <summary>Eventos publicados por Lançamentos neste cenário.</summary>
    public SondaDeEventos Eventos => infraestrutura.Eventos;

    public Resposta UltimaResposta => Ultima ?? throw new InvalidOperationException("Nenhuma requisição foi feita no cenário.");

    public HttpClient Lancamentos => infraestrutura.Lancamentos;

    public HttpClient Posicao => infraestrutura.Posicao;

    public string Token(string? titular = null, string escopos = EmissorDeTokens.Emissor.EscoposPadrao, TimeSpan? validade = null) =>
        EmissorDeTokens.Emissor.Emitir(Ambiente.Chave, Ambiente.Emissor, Ambiente.Audiencia, titular ?? Titular, escopos, validade ?? TimeSpan.FromMinutes(30));

    /// <inheritdoc cref="ScenarioContextInfrastructure.DerrubarCacheAsync"/>
    public Task DerrubarCacheAsync() => infraestrutura.DerrubarCacheAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.IniciarWorkerAsync"/>
    public Task IniciarWorkerAsync() => infraestrutura.IniciarWorkerAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.PararWorkerAsync"/>
    public Task PararWorkerAsync() => infraestrutura.PararWorkerAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.ReconstruirAsync"/>
    public Task<int> ReconstruirAsync() => infraestrutura.ReconstruirAsync();

    public Task PublicarAsync(string contrato, JsonObject mensagem, Guid? messageId = null) =>
        infraestrutura.PublicarAsync(contrato, mensagem, messageId);

    public Task PararBrokerAsync() => infraestrutura.PararBrokerAsync();

    public Task SubirBrokerAsync() => infraestrutura.SubirBrokerAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.TornarBancoDeLancamentosIndisponivelAsync"/>
    public Task TornarBancoDeLancamentosIndisponivelAsync() => infraestrutura.TornarBancoDeLancamentosIndisponivelAsync();

    public Task TornarBancoDeLancamentosDisponivelAsync() => infraestrutura.TornarBancoDeLancamentosDisponivelAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.TornarBancoDePosicaoIndisponivelAsync"/>
    public Task TornarBancoDePosicaoIndisponivelAsync() => infraestrutura.TornarBancoDePosicaoIndisponivelAsync();

    public Task TornarBancoDePosicaoDisponivelAsync() => infraestrutura.TornarBancoDePosicaoDisponivelAsync();

    /// <inheritdoc cref="ScenarioContextInfrastructure.ContarMensagensComFalhaAsync"/>
    public Task<uint> ContarMensagensComFalhaAsync(string consumidor) => infraestrutura.ContarMensagensComFalhaAsync(consumidor);

    /// <inheritdoc cref="ScenarioContextInfrastructure.ConectarAoBrokerAsync"/>
    public Task<IConnection> ConectarAoBrokerAsync() => infraestrutura.ConectarAoBrokerAsync();

    public async Task<Resposta> EnviarAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string caminho,
        object? corpo = null,
        string? chave = null,
        string? token = null,
        bool autenticar = true)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        using var requisicao = new HttpRequestMessage(metodo, new Uri(caminho, UriKind.Relative));
        if (autenticar)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? Token());
        }

        if (chave is not null)
        {
            requisicao.Headers.Add("Idempotency-Key", chave);
        }

        if (corpo is not null)
        {
            requisicao.Content = corpo as HttpContent ?? JsonContent.Create(corpo);
        }

        using var resposta = await cliente.SendAsync(requisicao);
        Ultima = await Resposta.LerAsync(resposta);
        return Ultima;
    }
}
