namespace Posicao.Domain.Comum;

public readonly record struct ContaId(Guid Valor)
{
    public override string ToString() => Valor.ToString();
}
