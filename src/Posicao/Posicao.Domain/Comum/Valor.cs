namespace Posicao.Domain.Comum;

/// <summary>
/// Quantia de um movimento em BRL: maior que zero, no máximo 999.999.999.999,99 e com até duas casas decimais.
/// Mesmas regras do <c>Valor</c> de Lançamentos, com código próprio (sem Shared Kernel). Saldos e totais não são
/// <see cref="Valor"/>, porque podem ser zero ou negativos.
/// </summary>
public readonly record struct Valor
{
    public const string Moeda = "BRL";
    public const decimal Maximo = 999_999_999_999.99m;

    private Valor(decimal quantia)
    {
        Quantia = quantia;
    }

    public decimal Quantia { get; }

    public static Resultado<Valor> Criar(decimal quantia) =>
        quantia <= 0 || quantia > Maximo || decimal.Round(quantia, 2) != quantia
            ? Resultado.Falhar<Valor>(CodigosDeFalha.ValorInvalido, "O valor deve ser maior que zero, com até duas casas decimais e no máximo 999999999999.99.")
            : Resultado.Ok(new Valor(Dinheiro.DuasCasas(quantia)));

    public override string ToString() => $"{Quantia:0.00} {Moeda}";
}
