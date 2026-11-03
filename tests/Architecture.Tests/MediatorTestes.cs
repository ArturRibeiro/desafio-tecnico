namespace Architecture.Tests;

/// <summary>Casos de uso só são chamados pelo Mediator, e cada pedido tem exatamente um handler.</summary>
public sealed class MediatorTestes
{
    public static TheoryData<string> Contextos => ["Lancamentos", "Posicao"];

    [Theory(DisplayName = "Todo command e query do Mediator tem exatamente um handler")]
    [MemberData(nameof(Contextos))]
    public void TodoPedido_TemExatamenteUmHandler(string contexto)
    {
        var aplicacao = Solucao.Assembly($"{contexto}.Application");
        var pedidos = aplicacao.GetTypes().Where(tipo => !tipo.IsAbstract && Contratos(tipo, "IRequest`1").Any()).ToList();
        var tratados = aplicacao.GetTypes()
            .Where(tipo => !tipo.IsAbstract)
            .SelectMany(tipo => Contratos(tipo, "IRequestHandler`2"))
            .Select(contrato => contrato.GetGenericArguments()[0])
            .ToList();

        pedidos.Should().NotBeEmpty();
        pedidos.ToList().ForEach(pedido => tratados.Should().ContainSingle(tratado => tratado == pedido));
    }

    [Theory(DisplayName = "Borda chama os casos de uso só pelo Mediator, nunca um handler diretamente")]
    [MemberData(nameof(Contextos))]
    public void Borda_ChamaOsCasosDeUsoSoPeloMediator(string contexto)
    {
        var handlers = Solucao.Assembly($"{contexto}.Application").GetTypes()
            .Where(tipo => !tipo.IsAbstract && Contratos(tipo, "IRequestHandler`2").Any())
            .Select(tipo => tipo.FullName!)
            .ToArray();
        var bordas = Solucao.Projetos.Where(p => p.Pasta == contexto && p.EhBorda).Select(p => p.Nome).ToList();

        handlers.Should().NotBeEmpty();
        bordas.ToList().ForEach(borda =>
        {
            var resultado = Types.InAssembly(Solucao.Assembly(borda)).ShouldNot().HaveDependencyOnAny(handlers).GetResult();
            resultado.IsSuccessful.Should().BeTrue($"{borda} usa um handler diretamente: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
        });
    }

    /// <summary>As respostas da API ficam em UseCases/.../&lt;Feature&gt;/Imp/Responses, com o sufixo Response; a borda não declara as suas.</summary>
    [Theory(DisplayName = "Respostas da API ficam em UseCases/.../<Feature>/Imp/Responses, com o sufixo Response")]
    [MemberData(nameof(Contextos))]
    public void Respostas_FicamEmImpResponses(string contexto)
    {
        var respostas = Solucao.Assembly($"{contexto}.Application").GetTypes()
            .Where(tipo => !tipo.IsNested && tipo.Namespace?.EndsWith(".Imp.Responses", StringComparison.Ordinal) == true)
            .ToList();
        var naApi = Solucao.Assembly($"{contexto}.Api").GetTypes()
            .Where(tipo => !tipo.IsNested && (tipo.Name.EndsWith("Response", StringComparison.Ordinal) || tipo.Name.EndsWith("Resposta", StringComparison.Ordinal)))
            .Select(tipo => tipo.Name)
            .ToList();

        respostas.Should().NotBeEmpty();
        respostas.ToList().ForEach(tipo => tipo.Name.Should().EndWith("Response"));
        (naApi.Count == 0).Should().BeTrue($"Respostas declaradas na {contexto}.Api: {string.Join(", ", naApi)}");
    }

    [Theory(DisplayName = "Pedidos do Mediator têm o sufixo Command ou Query")]
    [MemberData(nameof(Contextos))]
    public void Pedidos_TemOSufixoCommandOuQuery(string contexto)
    {
        var pedidos = Solucao.Assembly($"{contexto}.Application").GetTypes()
            .Where(tipo => !tipo.IsAbstract && Contratos(tipo, "IRequest`1").Any())
            .ToList();

        pedidos.Should().NotBeEmpty();
        pedidos.ToList().ForEach(tipo => tipo.Name.Should().MatchRegex("(Command|Query)$"));
    }

    [Theory(DisplayName = "Handlers do Mediator têm o sufixo Handler")]
    [MemberData(nameof(Contextos))]
    public void Handlers_TemOSufixoHandler(string contexto)
    {
        var handlers = Solucao.Assembly($"{contexto}.Application").GetTypes()
            .Where(tipo => tipo.IsClass && Contratos(tipo, "IRequestHandler`2").Any())
            .ToList();

        handlers.Should().NotBeEmpty();
        handlers.ToList().ForEach(tipo => tipo.Name.Should().EndWith("Handler"));
    }

    /// <summary>
    /// Toda leitura de um handler de Query é feita pelo IDbContextReadOnly, em LINQ, e nunca por um Repository, que fica com
    /// as escritas. Vale também para os serviços da Application que o handler recebe, e para os que eles recebem.
    /// </summary>
    [Theory(DisplayName = "Handlers de Query leem pelo IDbContextReadOnly, nunca por Repository")]
    [MemberData(nameof(Contextos))]
    public void HandlersDeQuery_LeemPeloIDbContextReadOnly(string contexto)
    {
        var aplicacao = Solucao.Assembly($"{contexto}.Application");
        var handlers = aplicacao.GetTypes()
            .Where(tipo => !tipo.IsAbstract && Contratos(tipo, "IRequestHandler`2").Any(c => c.GetGenericArguments()[0].Name.EndsWith("Query", StringComparison.Ordinal)))
            .ToList();

        handlers.ForEach(handler =>
        {
            var dependencias = Dependencias(handler, aplicacao, []).ToList();
            dependencias.Should().NotContain(tipo => tipo.Name.EndsWith("Repository", StringComparison.Ordinal), $"{handler.Name} lê pelo IDbContextReadOnly, não por Repository");
            dependencias.Should().Contain(tipo => tipo.Name == "IDbContextReadOnly", $"{handler.Name} lê pelo IDbContextReadOnly");
        });
    }

    /// <summary>
    /// Os casos de uso se dividem em UseCases/Queries/&lt;Feature&gt;/ (consultas, que só leem) e UseCases/Writings/&lt;Feature&gt;/
    /// (escritas, que alteram estado); o que é compartilhado fica em UseCases/Comum/. Em cada um, a raiz guarda as
    /// abstrações, Imp/ as implementações concretas (o handler, o Command ou a Query que ele trata e os serviços que ele
    /// usa) e Imp/Responses/ as respostas da API. As pastas são os namespaces.
    /// </summary>
    [Theory(DisplayName = "UseCases seguem a organização Queries|Writings/<Feature>, <Feature>/Imp e <Feature>/Imp/Responses")]
    [MemberData(nameof(Contextos))]
    public void UseCases_SeguemFeatureEImp(string contexto)
    {
        var raiz = $"{contexto}.Application.UseCases";
        var tipos = Solucao.Assembly($"{contexto}.Application").GetTypes()
            .Where(tipo => !tipo.IsNested && tipo.Namespace?.StartsWith(raiz, StringComparison.Ordinal) == true)
            .Where(tipo => tipo.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null)
            .ToList();

        tipos.ToList().ForEach(tipo => (tipo.Namespace!).Should().MatchRegex($@"^{Regex.Escape(raiz)}\.(Comum|(Queries|Writings)\.\w+)(\.Imp(\.Responses)?)?$"));
        tipos.Where(tipo => !tipo.IsAbstract && Contratos(tipo, "IRequestHandler`2").Any()).ToList().ForEach(handler =>
        {
            handler.Namespace.Should().EndWith(".Imp");
            var pedido = Contratos(handler, "IRequestHandler`2").Single().GetGenericArguments()[0];
            pedido.Namespace.Should().Be(handler.Namespace);
            var esperada = pedido.Name.EndsWith("Query", StringComparison.Ordinal) ? "Queries" : "Writings";
            handler.Namespace.Should().StartWith($"{raiz}.{esperada}.", $"{pedido.Name} é {(esperada == "Queries" ? "uma consulta" : "uma escrita")}");
        });

        var pasta = Path.Combine(Solucao.Diretorio.FullName, "src", contexto, $"{contexto}.Application", "UseCases");
        var foraDoLugar = Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories)
            .Select(arquivo => Path.GetRelativePath(pasta, arquivo).Split(Path.DirectorySeparatorChar))
            .Where(partes => !NoLugar(partes))
            .Select(partes => string.Join('/', partes))
            .ToList();
        (foraDoLugar.Count == 0).Should().BeTrue($"Arquivos fora de UseCases/Comum/ ou UseCases/Queries|Writings/<Feature>/ (com Imp/ e Imp/Responses/): {string.Join(", ", foraDoLugar)}");
    }

    /// <summary>Caminho relativo a UseCases/: Comum/[Imp/[Responses/]]arquivo ou (Queries|Writings)/&lt;Feature&gt;/[Imp/[Responses/]]arquivo.</summary>
    private static bool NoLugar(string[] partes)
    {
        var feature = partes[0] switch
        {
            "Comum" => partes[1..],
            "Queries" or "Writings" when partes.Length > 2 => partes[2..],
            _ => null,
        };
        return feature switch
        {
            null => false,
            [_] => true,
            ["Imp", _] => true,
            ["Imp", "Responses", _] => true,
            _ => false,
        };
    }

    /// <summary>Tipos recebidos nos construtores, descendo pelas classes da própria Application.</summary>
    private static IEnumerable<Type> Dependencias(Type tipo, Assembly aplicacao, HashSet<Type> visitados) =>
        !visitados.Add(tipo)
            ? []
            : tipo.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
                .SelectMany(dependencia => dependencia is { IsClass: true } && dependencia.Assembly == aplicacao
                    ? Dependencias(dependencia, aplicacao, visitados).Prepend(dependencia)
                    : [dependencia]);

    private static IEnumerable<Type> Contratos(Type tipo, string nome) =>
        tipo.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == nome
            && i.Namespace == "Plataforma.Mediator");
}
