namespace Posicao.Domain.Comum;

public static class Dinheiro
{
    /// <summary>Sempre com duas casas: 30 vira 30.00, para o mesmo valor gerar o mesmo texto em memória e relido do banco.</summary>
    public static decimal DuasCasas(decimal valor) => decimal.Round(valor, 2) + 0.00m;
}
