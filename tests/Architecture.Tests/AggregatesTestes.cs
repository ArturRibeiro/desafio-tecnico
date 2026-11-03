namespace Architecture.Tests;

/// <summary>
/// Regra de DDD (DOMINIO.md, seção 4.3): só Aggregate Root tem Repository. Um Aggregate Root implementa a interface
/// marcadora IAggregateRoot do seu Domain, e a interface do Repository se chama I&lt;Aggregate&gt;Repository.
/// </summary>
public sealed class AggregatesTestes
{
    public static TheoryData<string> Contextos => ["Lancamentos", "Posicao"];

    [Theory(DisplayName = "Todo Repository é de um Aggregate Root")]
    [MemberData(nameof(Contextos))]
    public void TodoRepository_EDeUmAggregateRoot(string contexto)
    {
        var (raiz, tipos) = Dominio(contexto);
        var repositorios = tipos.Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal)).ToList();

        repositorios.Should().NotBeEmpty();
        foreach (var repositorio in repositorios)
        {
            var nome = repositorio.Name[1..^"Repository".Length];
            var aggregate = tipos.SingleOrDefault(t => t.IsClass && t.Name == nome);
            aggregate.Should().NotBeNull($"{repositorio.Name} precisa de um tipo {nome} no {contexto}.Domain");
            aggregate!.IsAssignableTo(raiz).Should().BeTrue($"{repositorio.Name} é de {nome}, que precisa ser um Aggregate Root (IAggregateRoot)");
        }
    }

    [Theory(DisplayName = "Todo Aggregate Root tem um único Repository, e nenhum outro Repository o lê ou grava")]
    [MemberData(nameof(Contextos))]
    public void TodoAggregateRoot_TemUmUnicoRepository(string contexto)
    {
        var (raiz, tipos) = Dominio(contexto);
        var aggregates = tipos.Where(t => t.IsClass && t.IsAssignableTo(raiz)).ToList();
        var repositorios = tipos.Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal)).ToList();

        aggregates.Should().NotBeEmpty();
        foreach (var aggregate in aggregates)
        {
            var proprio = $"I{aggregate.Name}Repository";
            repositorios.Count(r => r.Name == proprio).Should().Be(1, $"o Aggregate Root {aggregate.Name} precisa de um único {proprio}");

            var outros = repositorios.Where(r => r.Name != proprio && TiposNasAssinaturas(r).Contains(aggregate)).Select(r => r.Name).ToList();
            outros.Should().BeEmpty($"só {proprio} lê ou grava {aggregate.Name}");
        }
    }

    [Theory(DisplayName = "Cada interface de Repository tem uma única implementação na Infrastructure")]
    [MemberData(nameof(Contextos))]
    public void CadaRepository_TemUmaUnicaImplementacao(string contexto)
    {
        var (_, tipos) = Dominio(contexto);
        var implementacoes = Solucao.Assembly($"{contexto}.Infrastructure").GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToList();

        foreach (var repositorio in tipos.Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal)))
        {
            var deste = implementacoes.Where(t => t.IsAssignableTo(repositorio)).Select(t => t.Name).ToList();
            deste.Should().ContainSingle($"{repositorio.Name} precisa de uma única implementação, e não de {string.Join(", ", deste)}");
        }
    }

    /// <summary>Tipos que aparecem nos retornos e parâmetros dos métodos, abrindo genéricos (Task&lt;T?&gt;, IReadOnlyList&lt;T&gt;).</summary>
    private static HashSet<Type> TiposNasAssinaturas(Type repositorio) =>
        repositorio.GetMethods()
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .SelectMany(Abrir)
            .ToHashSet();

    private static IEnumerable<Type> Abrir(Type tipo) =>
        tipo.IsGenericType ? tipo.GetGenericArguments().SelectMany(Abrir).Append(tipo) : [Nullable.GetUnderlyingType(tipo) ?? tipo];

    private static (Type Raiz, List<Type> Tipos) Dominio(string contexto)
    {
        var tipos = Solucao.Assembly($"{contexto}.Domain").GetTypes().ToList();
        return (tipos.Single(t => t.FullName == $"{contexto}.Domain.Comum.IAggregateRoot"), tipos);
    }
}
