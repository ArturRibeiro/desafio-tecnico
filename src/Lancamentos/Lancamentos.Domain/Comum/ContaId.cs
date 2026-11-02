namespace Lancamentos.Domain.Comum;

/// <summary>Identificador da conta: GUID v7, aleatório e não consecutivo.</summary>
public readonly record struct ContaId(Guid Valor)
{
    public static ContaId Novo() => new(Guid.CreateVersion7());

    public override string ToString() => Valor.ToString();
}
