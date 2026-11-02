namespace Architecture.Tests;

/// <param name="Nome">Nome do projeto, por exemplo Lancamentos.Domain.</param>
/// <param name="Pasta">Pasta de primeiro nível em src/: Lancamentos, Posicao, Gateway ou Compartilhado.</param>
/// <param name="Referencias">Nomes dos projetos referenciados diretamente.</param>
/// <param name="Pacotes">Pacotes NuGet e frameworks referenciados diretamente.</param>
internal sealed record Projeto(string Nome, string Pasta, IReadOnlyList<string> Referencias, IReadOnlyList<string> Pacotes)
{
    public bool EhBorda => Nome.EndsWith(".Api", StringComparison.Ordinal)
        || Nome.EndsWith(".Worker", StringComparison.Ordinal)
        || Nome == "Gateway";

    public override string ToString() => Nome;
}
