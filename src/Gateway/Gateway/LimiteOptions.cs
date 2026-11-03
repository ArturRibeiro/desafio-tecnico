namespace Gateway;

/// <summary>Opções da seção "LimiteDeRequisicoes" (variáveis LimiteDeRequisicoes__*).</summary>
internal sealed class LimiteOptions
{
    /// <summary>Requisições permitidas a cada cliente por janela.</summary>
    public int Permissoes { get; set; } = 100;

    public TimeSpan Janela { get; set; } = TimeSpan.FromSeconds(10);
}
