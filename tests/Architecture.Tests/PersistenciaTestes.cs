namespace Architecture.Tests;

/// <summary>
/// Convenção de persistência (openspec/config.yaml): todo mapeamento do EF Core é uma classe que implementa
/// IEntityTypeConfiguration&lt;T&gt;, uma por entidade e por arquivo, na pasta Persistencia/EntityConfigurations da
/// Infrastructure. O DbContext só aplica as configurações; não mapeia nada direto.
/// </summary>
public sealed partial class PersistenciaTestes
{
    private static readonly string[] CamadasForaDoDominio = ["Application", "Infrastructure"];

    public static TheoryData<string> Contextos => ["Lancamentos", "Posicao"];

    [Theory(DisplayName = "Mapeamentos ficam em EntityConfigurations, com o sufixo Configuration")]
    [MemberData(nameof(Contextos))]
    public void Mapeamentos_FicamEmEntityConfigurations(string contexto)
    {
        var pasta = $"{contexto}.Infrastructure.Persistencia.EntityConfigurations";

        var foraDaRegra = Configuracoes(contexto)
            .Where(c => c.Tipo.Namespace != pasta || c.Tipo.Name != $"{c.Entidade.Name}Configuration")
            .Select(c => c.Tipo.FullName)
            .ToList();

        (foraDaRegra.Count == 0).Should().BeTrue($"Mapeamentos fora de {pasta} ou sem o nome <Entidade>Configuration: {string.Join(", ", foraDaRegra)}");
    }

    [Theory(DisplayName = "Cada entidade do DbContext tem exatamente um IEntityTypeConfiguration")]
    [MemberData(nameof(Contextos))]
    public void CadaEntidade_TemUmaConfiguracao(string contexto)
    {
        var configuradas = Configuracoes(contexto).Select(c => c.Entidade).ToList();
        var entidades = DbContext(contexto).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToList();

        entidades.Should().NotBeEmpty();
        entidades.ForEach(entidade => configuradas.Count(c => c == entidade).Should().Be(1, $"{entidade.Name} precisa de um único IEntityTypeConfiguration"));
    }

    [Theory(DisplayName = "O DbContext não mapeia entidades direto no OnModelCreating")]
    [MemberData(nameof(Contextos))]
    public void DbContext_NaoMapeiaEntidadesDireto(string contexto)
    {
        var arquivo = Path.Combine(Solucao.Diretorio.FullName, "src", contexto, $"{contexto}.Infrastructure", "Persistencia", $"{DbContext(contexto).Name}.cs");

        File.ReadAllText(arquivo).Should().NotContain(".Entity<", "os mapeamentos ficam nas classes de EntityConfigurations");
    }

    [Theory(DisplayName = "Interfaces de Repository ficam no Domain, e as implementações em Persistencia/Repositories")]
    [MemberData(nameof(Contextos))]
    public void InterfacesDeRepository_FicamNoDomain(string contexto)
    {
        var dominio = Solucao.Assembly($"{contexto}.Domain");
        var foraDoDominio = CamadasForaDoDominio
            .SelectMany(camada => Solucao.Assembly($"{contexto}.{camada}").GetTypes())
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        var implementacoes = Solucao.Assembly($"{contexto}.Infrastructure").GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();

        (foraDoDominio.Count == 0).Should().BeTrue($"Interfaces de Repository fora do Domain: {string.Join(", ", foraDoDominio)}");
        implementacoes.Should().NotBeEmpty();
        implementacoes.ForEach(t => t.GetInterfaces().Should().Contain(i => i.Assembly == dominio && i.Name == $"I{t.Name}", $"{t.Name} implementa a interface I{t.Name} do Domain"));
        implementacoes.ForEach(t => t.Namespace.Should().Be($"{contexto}.Infrastructure.Persistencia.Repositories", $"{t.Name} fica na pasta Persistencia/Repositories"));
    }

    /// <summary>
    /// Todo acesso a dados passa pelos Repositories (escritas) ou pelo IDbContextReadOnly das Queries (leituras,
    /// em Persistencia/ReadOnly): fora deles, nenhum método lê uma propriedade DbSet do DbContext nem chama Set&lt;T&gt;(). A
    /// UnitOfWork usa só a transação e o SaveChanges, e as migrações e o health check, só o esquema (Database). A
    /// verificação é feita no IL, porque o NetArchTest não vê o tipo de retorno de uma propriedade.
    /// </summary>
    [Theory(DisplayName = "Só os Repositories e o IDbContextReadOnly das Queries acessam os dados (DbSet)")]
    [MemberData(nameof(Contextos))]
    public void SoOsRepositories_AcessamOsDados(string contexto)
    {
        string[] permitidos = [$"{contexto}.Infrastructure.Persistencia.Repositories", $"{contexto}.Infrastructure.Persistencia.ReadOnly"];
        var projetos = Solucao.Projetos.Where(p => p.Pasta == contexto && !p.Nome.EndsWith(".Domain", StringComparison.Ordinal));

        var foraDosRepositories = projetos
            .SelectMany(p => Mono.Cecil.AssemblyDefinition.ReadAssembly(Solucao.Assembly(p.Nome).Location).MainModule.GetTypes())
            .Where(t => !permitidos.Contains(Externo(t).Namespace) && !Externo(t).Name.EndsWith("DbContext", StringComparison.Ordinal))
            .SelectMany(t => t.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions.Select(i => (Tipo: t, Chamada: i.Operand as Mono.Cecil.MethodReference))))
            .Where(x => x.Chamada is not null && AcessaDbSet(x.Chamada))
            .Select(x => $"{x.Tipo.FullName} ({x.Chamada!.Name})")
            .Distinct()
            .ToList();

        (foraDosRepositories.Count == 0).Should().BeTrue($"Acesso a DbSet fora dos Repositories e do IDbContextReadOnly: {string.Join(", ", foraDosRepositories)}");
    }

    /// <summary>As consultas são escritas em LINQ: nenhum SQL à mão no código, só nas migrações (esquema).</summary>
    [Fact(DisplayName = "Acesso a dados só em LINQ, sem SQL escrito à mão (FromSql, SqlQuery, ExecuteSql)")]
    public void AcessoADados_SoEmLinq()
    {
        var comSql = Directory.EnumerateFiles(Path.Combine(Solucao.Diretorio.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => !arquivo.Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj" or "Migracoes"))
            .Where(arquivo => SqlEscritoAMao().IsMatch(File.ReadAllText(arquivo)))
            .Select(arquivo => Path.GetRelativePath(Solucao.Diretorio.FullName, arquivo))
            .ToList();

        (comSql.Count == 0).Should().BeTrue($"Arquivos com SQL escrito à mão: {string.Join(", ", comSql)}");
    }

    /// <summary>Leitura de uma propriedade que devolve DbSet&lt;T&gt;, ou chamada a DbContext.Set&lt;T&gt;().</summary>
    private static bool AcessaDbSet(Mono.Cecil.MethodReference chamada) =>
        (chamada.Name.StartsWith("get_", StringComparison.Ordinal) && chamada.ReturnType.Name == "DbSet`1")
        || (chamada.Name == "Set" && chamada.DeclaringType.FullName == "Microsoft.EntityFrameworkCore.DbContext");

    /// <summary>Tipo de primeiro nível: as máquinas de estado dos métodos async são tipos aninhados, sem namespace próprio.</summary>
    private static Mono.Cecil.TypeDefinition Externo(Mono.Cecil.TypeDefinition tipo) =>
        tipo.DeclaringType is null ? tipo : Externo(tipo.DeclaringType);

    private static Type DbContext(string contexto) =>
        Solucao.Assembly($"{contexto}.Infrastructure").GetTypes().Single(t => t.IsSubclassOf(typeof(Microsoft.EntityFrameworkCore.DbContext)));

    private static List<(Type Tipo, Type Entidade)> Configuracoes(string contexto) =>
        Solucao.Assembly($"{contexto}.Infrastructure").GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<>))
                .Select(i => (Tipo: t, Entidade: i.GetGenericArguments()[0])))
            .ToList();

    [GeneratedRegex(@"\b(FromSql|FromSqlRaw|FromSqlInterpolated|SqlQuery|SqlQueryRaw|ExecuteSql|ExecuteSqlRaw|ExecuteSqlInterpolated)(Async)?\b")]
    private static partial Regex SqlEscritoAMao();
}
