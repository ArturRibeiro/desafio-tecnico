namespace Posicao.Domain.Movimentos;

/// <summary>Sentido do movimento no saldo. O de um estorno é o inverso do lançamento que ele anula.</summary>
public enum Efeito
{
    Credito,
    Debito,
}
