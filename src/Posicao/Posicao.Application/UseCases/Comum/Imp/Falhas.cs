namespace Posicao.Application.UseCases.Comum.Imp;

internal static class Falhas
{
    /// <summary>Mesma falha para conta inexistente, de outro titular ou ainda não projetada (DOMINIO.md, seção 6.7).</summary>
    public static readonly Falha ContaNaoEncontrada = new(CodigosDeFalha.ContaNaoEncontrada, "Conta não encontrada.");
}
