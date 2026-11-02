namespace Posicao.Domain.Comum;

/// <summary>
/// Resultado de uma operação de negócio: um valor ou uma <see cref="Falha"/>.
/// Falhas de negócio não são exceções; exceções ficam para falhas técnicas.
/// </summary>
public sealed class Resultado<T>
{
    private readonly T? valor;

    internal Resultado(T valor)
    {
        this.valor = valor;
    }

    internal Resultado(Falha falha)
    {
        Falha = falha;
    }

    public Falha? Falha { get; }

    public bool Sucesso => Falha is null;

    public T Valor => Sucesso
        ? valor!
        : throw new InvalidOperationException($"Resultado com falha '{Falha!.Codigo}' não tem valor.");
}
