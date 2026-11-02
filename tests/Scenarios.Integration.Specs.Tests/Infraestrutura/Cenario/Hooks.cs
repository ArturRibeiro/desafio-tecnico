namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Cenario;

/// <summary>
/// Ciclo da execução e de cada cenário: os containers sobem uma vez por execução; antes de cada cenário, a
/// infraestrutura dele (bancos, virtual host, Redis limpo) é criada e Posicao.Worker, Lancamentos.Api e Posicao.Api
/// sobem com <see cref="WebApplicationFactory{TEntryPoint}"/>; depois do cenário, tudo é descartado e o broker volta
/// se o cenário o parou. Um passo sem implementação ou pendente falha o cenário, em vez de deixá-lo inconclusivo.
/// </summary>
[Binding]
public sealed class Hooks(ScenarioContextInfrastructure infraestrutura, TechTalk.SpecFlow.ScenarioContext cenario)
{
    /// <summary>Depois do descarte do cenário (ordem padrão 10000), para não deixar a infraestrutura dele para trás.</summary>
    private const int DepoisDoDescarte = 20000;

    [BeforeTestRun]
    public static Task SubirContainersAsync() => Ambiente.IniciarAsync();

    [AfterTestRun]
    public static Task DerrubarContainersAsync() => Ambiente.EncerrarAsync();

    [BeforeScenario]
    public Task SubirCenarioAsync() => infraestrutura.IniciarAsync();

    [AfterScenario]
    public async Task DerrubarCenarioAsync() => await infraestrutura.DisposeAsync();

    [AfterScenario(Order = DepoisDoDescarte)]
    public void FalharPassoSemImplementacao()
    {
        if (cenario.ScenarioExecutionStatus is ScenarioExecutionStatus.UndefinedStep or ScenarioExecutionStatus.StepDefinitionPending)
        {
            throw new InvalidOperationException(
                $"O cenário \"{cenario.ScenarioInfo.Title}\" tem passo sem implementação ({cenario.ScenarioExecutionStatus}).");
        }
    }
}
