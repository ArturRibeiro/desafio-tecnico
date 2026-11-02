namespace Architecture.Tests;

/// <summary>
/// Lê a estrutura real da solução: os .csproj de src/ e os assemblies compilados.
/// As referências são lidas dos .csproj porque o compilador descarta do assembly
/// as referências sem uso de tipos, e uma referência de projeto já é uma dependência.
/// </summary>
internal static class Solucao
{
    private static readonly Lazy<DirectoryInfo> Raiz = new(EncontrarRaiz);

    public static IReadOnlyList<Projeto> Projetos { get; } = CarregarProjetos();

    /// <summary>Pasta raiz da solução, onde está o DesafioTecnico.slnx.</summary>
    public static DirectoryInfo Diretorio => Raiz.Value;

    public static Projeto Projeto(string nome) =>
        Projetos.SingleOrDefault(p => p.Nome == nome)
        ?? throw new InvalidOperationException($"Projeto '{nome}' não encontrado em src/.");

    public static Assembly Assembly(string nome) => System.Reflection.Assembly.Load(nome);

    private static List<Projeto> CarregarProjetos()
    {
        var src = Path.Combine(Raiz.Value.FullName, "src");
        return Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(Ler)
            .OrderBy(p => p.Nome, StringComparer.Ordinal)
            .ToList();
    }

    private static Projeto Ler(string caminho)
    {
        var xml = XDocument.Load(caminho);
        var diretorio = Path.GetDirectoryName(caminho)!;
        var referencias = xml.Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();
        var pacotes = xml.Descendants("PackageReference")
            .Select(r => r.Attribute("Include")!.Value)
            .Concat(xml.Descendants("FrameworkReference").Select(r => r.Attribute("Include")!.Value))
            .ToList();
        var pasta = Path.GetRelativePath(Path.Combine(Raiz.Value.FullName, "src"), diretorio).Split(Path.DirectorySeparatorChar)[0];
        return new Projeto(Path.GetFileNameWithoutExtension(caminho), pasta, referencias, pacotes);
    }

    private static DirectoryInfo EncontrarRaiz()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "DesafioTecnico.slnx")))
        {
            atual = atual.Parent;
        }

        return atual ?? throw new InvalidOperationException("Raiz da solução (DesafioTecnico.slnx) não encontrada.");
    }
}
