namespace Architecture.Tests;

/// <summary>Convenções de nomes e de organização do código (openspec/config.yaml).</summary>
public sealed partial class ConvencoesTestes
{
    private static readonly string[] Camadas = ["Domain", "Application", "Infrastructure", "Api"];
    private static readonly string[] PastasDeCodigo = ["src", "tests", "ferramentas"];

    public static TheoryData<string> Contextos => ["Lancamentos", "Posicao"];

    [Theory(DisplayName = "Projetos de um contexto seguem o nome das camadas (Domain, Application, Infrastructure, Api)")]
    [MemberData(nameof(Contextos))]
    public void ProjetosDeUmContexto_SeguemONomeDasCamadas(string contexto)
    {
        var permitidos = Camadas.Append("Worker").Select(c => $"{contexto}.{c}").ToHashSet(StringComparer.Ordinal);
        var projetos = Solucao.Projetos.Where(p => p.Pasta == contexto).Select(p => p.Nome).ToList();

        projetos.ToList().ForEach(nome => permitidos.Should().Contain(nome));
        Camadas.ToList().ForEach(camada => projetos.Should().Contain($"{contexto}.{camada}"));
    }

    /// <summary>
    /// Um tipo por arquivo, nomeado pelo tipo (genérico: Resultado{T}.cs). Com namespaces de arquivo (exigidos pelo
    /// .editorconfig), os tipos de primeiro nível começam na coluna 0; os aninhados ficam indentados e não contam.
    /// O Program.cs (instruções de nível superior) e as migrações geradas pelo EF Core ficam de fora.
    /// </summary>
    [Fact(DisplayName = "Cada arquivo de código tem um único tipo, com o nome do arquivo")]
    public void Codigo_TemUmTipoPorArquivo()
    {
        var foraDaRegra = ArquivosDeCodigo()
            .Where(arquivo => Path.GetFileName(arquivo) != "Program.cs")
            .Where(arquivo => !arquivo.Split(Path.DirectorySeparatorChar).Contains("Migracoes"))
            .Select(arquivo => (Arquivo: arquivo, Tipos: File.ReadLines(arquivo).Select(linha => DeclaracaoDeTipo().Match(linha)).Where(m => m.Success).Select(NomeDoArquivo).ToList()))
            .Where(item => item.Tipos.Count != 1 || item.Tipos[0] != Path.GetFileNameWithoutExtension(item.Arquivo))
            .Select(item => $"{Path.GetRelativePath(Solucao.Diretorio.FullName, item.Arquivo)} [{string.Join(", ", item.Tipos)}]")
            .ToList();

        (foraDaRegra.Count == 0).Should().BeTrue($"Arquivos fora da regra de um tipo por arquivo: {string.Join("; ", foraDaRegra)}");
    }

    /// <summary>Projetos de teste: &lt;Area&gt;.Unit.Tests, Architecture.Tests ou Scenarios.Integration.Specs.Tests (cenários das specs de negócio).</summary>
    [Fact(DisplayName = "Projetos de teste seguem o nome por tipo (Unit, Architecture ou cenários das specs)")]
    public void ProjetosDeTeste_SeguemONomePorTipo()
    {
        var foraDoPadrao = Directory.EnumerateFiles(Path.Combine(Solucao.Diretorio.FullName, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(nome => !NomeDeProjetoDeTeste().IsMatch(nome!))
            .ToList();

        (foraDoPadrao.Count == 0).Should().BeTrue($"Projetos de teste fora do padrão: {string.Join(", ", foraDoPadrao)}");
    }

    /// <summary>
    /// No Scenarios.Integration.Specs.Tests, as classes de passos (com [Given], [When] ou [Then]) ficam na pasta Steps, e toda classe
    /// dessa pasta tem o sufixo Step. Com um tipo por arquivo, o nome do arquivo é o nome da classe.
    /// </summary>
    [Fact(DisplayName = "Passos dos cenários ficam na pasta Steps, em classes com o sufixo Step")]
    public void PassosDosCenarios_FicamEmStepsComSufixoStep()
    {
        var projeto = Path.Combine(Solucao.Diretorio.FullName, "tests", "Scenarios.Integration.Specs.Tests");
        var steps = Path.Combine(projeto, "Steps") + Path.DirectorySeparatorChar;
        var foraDaRegra = ArquivosDeCodigo()
            .Where(arquivo => arquivo.StartsWith(projeto + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(arquivo => arquivo.StartsWith(steps, StringComparison.Ordinal)
                ? !Path.GetFileNameWithoutExtension(arquivo).EndsWith("Step", StringComparison.Ordinal)
                : File.ReadLines(arquivo).Any(linha => AtributoDePasso().IsMatch(linha)))
            .Select(arquivo => Path.GetRelativePath(Solucao.Diretorio.FullName, arquivo))
            .ToList();

        (foraDaRegra.Count == 0).Should().BeTrue($"Passos fora de Steps ou sem o sufixo Step: {string.Join(", ", foraDaRegra)}");
    }

    /// <summary>Os usings de cada projeto ficam todos no Usings.cs dele, como global using.</summary>
    [Fact(DisplayName = "Usings de cada projeto ficam só no Usings.cs, como global using")]
    public void Usings_FicamSoNoUsingsCs()
    {
        var foraDoLugar = ArquivosDeCodigo()
            .Where(arquivo => File.ReadLines(arquivo).Any(linha => DiretivaUsing().IsMatch(linha)))
            .Select(arquivo => Path.GetRelativePath(Solucao.Diretorio.FullName, arquivo))
            .ToList();

        (foraDoLugar.Count == 0).Should().BeTrue($"Arquivos com using fora do Usings.cs: {string.Join(", ", foraDoLugar)}");
    }

    /// <summary>
    /// Arquivos .cs escritos à mão em src/, tests/ e ferramentas/, sem bin/, obj/, os Usings.cs e o código que o SpecFlow
    /// gera ao lado de cada .feature (*.feature.cs).
    /// </summary>
    /// <summary>
    /// Classes que implementam um padrão levam o nome dele em inglês (IContaRepository, IUnitOfWork, BancoOptions...).
    /// Com um tipo por arquivo, basta olhar os nomes dos arquivos; as migrações geradas e as pastas não entram.
    /// </summary>
    [Fact(DisplayName = "Nomes de padrões não são traduzidos (Repository, UnitOfWork, Options, Factory...)")]
    public void NomesDePadroes_NaoSaoTraduzidos()
    {
        var traduzidos = ArquivosDeCodigo()
            .Where(arquivo => !arquivo.Split(Path.DirectorySeparatorChar).Contains("Migracoes"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(nome => PadraoTraduzido().IsMatch(nome!))
            .ToList();

        (traduzidos.Count == 0).Should().BeTrue($"Tipos com o nome do padrão traduzido: {string.Join(", ", traduzidos)}");

        var pastas = Directory.EnumerateDirectories(Path.Combine(Solucao.Diretorio.FullName, "src"), "Portas", SearchOption.AllDirectories).ToList();
        (pastas.Count == 0).Should().BeTrue($"As portas ficam na pasta Ports: {string.Join(", ", pastas)}");
    }

    private static IEnumerable<string> ArquivosDeCodigo() =>
        PastasDeCodigo
            .SelectMany(pasta => Directory.EnumerateFiles(Path.Combine(Solucao.Diretorio.FullName, pasta), "*.cs", SearchOption.AllDirectories))
            .Where(arquivo => !arquivo.Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj"))
            .Where(arquivo => Path.GetFileName(arquivo) != "Usings.cs")
            .Where(arquivo => !arquivo.EndsWith(".feature.cs", StringComparison.Ordinal));

    /// <summary>Nome{T} ou Nome{TRequest,TResponse}: só os nomes dos parâmetros, sem espaços nem in/out.</summary>
    private static string NomeDoArquivo(Match declaracao) =>
        declaracao.Groups["genericos"].Success
            ? $"{declaracao.Groups["nome"].Value}{{{string.Join(',', declaracao.Groups["genericos"].Value.Split(',').Select(p => p.Trim().Split(' ')[^1]))}}}"
            : declaracao.Groups["nome"].Value;

    [GeneratedRegex(@"^([A-Z]\w*\.)+Unit\.Tests$|^Architecture\.Tests$|^Scenarios\.Integration\.Specs\.Tests$")]
    private static partial Regex NomeDeProjetoDeTeste();

    [GeneratedRegex(@"^\uFEFF?(?:(?:public|internal|file|sealed|static|abstract|readonly|partial)\s+)*(?:class|record\s+struct|record|struct|enum|interface)\s+(?<nome>\w+)(?:<(?<genericos>[^>(]+)>)?")]
    private static partial Regex DeclaracaoDeTipo();

    [GeneratedRegex(@"^﻿?\s*(global\s+)?using\s+(static\s+)?[A-Za-z_][\w.]*\s*(=\s*[^;]+)?;")]
    private static partial Regex DiretivaUsing();

    [GeneratedRegex(@"^\s*\[(Given|When|Then)\(")]
    private static partial Regex AtributoDePasso();

    /// <summary>Nomes de padrões em português que não devem aparecer no nome de um tipo.</summary>
    [GeneratedRegex("Repositorio|UnidadeDeTrabalho|Fabrica|Tradutor|Consumidor|Opcoes|Publicador|Adaptador|Conversor|Aplicador|Falso|VerificacaoDe|SemCache|CacheDe")]
    private static partial Regex PadraoTraduzido();
}
