namespace Architecture.Tests;

/// <summary>
/// Specs "Domínio isolado" e "Dependências entre camadas apontam para dentro" (a Infraestrutura pode usar os contratos de integração).
/// </summary>
public sealed class CamadasTestes
{
    private static readonly string[] FrameworksDeInfraestrutura =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.Hosting",
        "MassTransit",
        "Npgsql",
        "StackExchange.Redis",
        "Yarp",
    ];

    public static TheoryData<string> Contextos => ["Lancamentos", "Posicao"];

    [Theory(DisplayName = "Domínio não referencia projetos nem pacotes")]
    [MemberData(nameof(Contextos))]
    public void Dominio_NaoReferenciaProjetosNemPacotes(string contexto)
    {
        var dominio = Solucao.Projeto($"{contexto}.Domain");

        dominio.Referencias.Should().BeEmpty();
        dominio.Pacotes.Should().BeEmpty();
    }

    [Theory(DisplayName = "Domínio não depende de outras camadas nem de frameworks")]
    [MemberData(nameof(Contextos))]
    public void Dominio_NaoDependeDeOutrasCamadasNemDeFrameworks(string contexto)
    {
        var proibidos = FrameworksDeInfraestrutura
            .Concat([$"{contexto}.Application", $"{contexto}.Infrastructure", $"{contexto}.Api", $"{contexto}.Worker"])
            .ToArray();

        var resultado = Types.InAssembly(Solucao.Assembly($"{contexto}.Domain"))
            .ShouldNot().HaveDependencyOnAny(proibidos)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(Descrever(resultado));
    }

    [Theory(DisplayName = "Aplicação referencia apenas o domínio e o Mediator")]
    [MemberData(nameof(Contextos))]
    public void Aplicacao_ReferenciaApenasODominioEOMediator(string contexto)
    {
        var aplicacao = Solucao.Projeto($"{contexto}.Application");

        aplicacao.Referencias.ToList().ForEach(r => (new[] { $"{contexto}.Domain", "Plataforma.Mediator" }).Should().Contain(r));
    }

    [Theory(DisplayName = "Aplicação não depende da infraestrutura nem da borda")]
    [MemberData(nameof(Contextos))]
    public void Aplicacao_NaoDependeDeInfraestruturaNemDaBorda(string contexto)
    {
        var resultado = Types.InAssembly(Solucao.Assembly($"{contexto}.Application"))
            .ShouldNot().HaveDependencyOnAny($"{contexto}.Infrastructure", $"{contexto}.Api", $"{contexto}.Worker")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(Descrever(resultado));
    }

    [Theory(DisplayName = "Infraestrutura referencia apenas aplicação, domínio e contratos de integração")]
    [MemberData(nameof(Contextos))]
    public void Infraestrutura_ReferenciaApenasAplicacaoDominioEContratos(string contexto)
    {
        var infraestrutura = Solucao.Projeto($"{contexto}.Infrastructure");
        string[] permitidos = [$"{contexto}.Application", $"{contexto}.Domain", "Contratos.Integracao"];

        infraestrutura.Referencias.ToList().ForEach(r => permitidos.Should().Contain(r));
    }

    /// <summary>O modelo interno não conhece a Published Language: a tradução fica na Infraestrutura.</summary>
    [Theory(DisplayName = "Domínio e aplicação não dependem dos contratos de integração")]
    [MemberData(nameof(Contextos))]
    public void DominioEAplicacao_NaoDependemDosContratosDeIntegracao(string contexto)
    {
        foreach (var camada in new[] { "Domain", "Application" })
        {
            Solucao.Projeto($"{contexto}.{camada}").Referencias.Should().NotContain("Contratos.Integracao");

            var resultado = Types.InAssembly(Solucao.Assembly($"{contexto}.{camada}"))
                .ShouldNot().HaveDependencyOn("Contratos.Integracao")
                .GetResult();
            resultado.IsSuccessful.Should().BeTrue(Descrever(resultado));
        }
    }

    [Theory(DisplayName = "Infraestrutura não depende da borda")]
    [MemberData(nameof(Contextos))]
    public void Infraestrutura_NaoDependeDaBorda(string contexto)
    {
        var resultado = Types.InAssembly(Solucao.Assembly($"{contexto}.Infrastructure"))
            .ShouldNot().HaveDependencyOnAny($"{contexto}.Api", $"{contexto}.Worker")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(Descrever(resultado));
    }

    internal static string Descrever(NetArchTest.Rules.TestResult resultado) =>
        "Tipos com dependência proibida: " + string.Join(", ", resultado.FailingTypeNames ?? []);
}
