namespace Lancamentos.Domain.Comum;

/// <summary>Identificador do lançamento: GUID v7, aleatório e não consecutivo.</summary>
public readonly record struct LancamentoId(Guid Valor)
{
    public static LancamentoId Novo() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}
