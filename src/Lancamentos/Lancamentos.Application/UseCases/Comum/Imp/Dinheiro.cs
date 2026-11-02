namespace Lancamentos.Application.UseCases.Comum.Imp;

internal static class Dinheiro
{
    /// <summary>Sempre com duas casas: o mesmo valor calculado em memória ou relido do banco gera o mesmo JSON.</summary>
    public static decimal DuasCasas(decimal valor) => decimal.Round(valor, 2) + 0.00m;
}
