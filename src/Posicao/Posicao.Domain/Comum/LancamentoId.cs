namespace Posicao.Domain.Comum;

public readonly record struct LancamentoId(Guid Valor)
{
    public override string ToString() => Valor.ToString();
}
