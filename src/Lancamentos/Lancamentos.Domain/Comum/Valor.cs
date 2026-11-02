namespace Lancamentos.Domain.Comum;

/// <summary>
/// Quantia de um lançamento em BRL: maior que zero, no máximo 999.999.999.999,99 e com até duas casas decimais.
/// Nunca é arredondada: 10.005 é recusado, 10.500 é aceito.
/// Saldo e limite não são <see cref="Valor"/>, porque podem ser zero ou negativos.
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
            : Resultado.Ok(new Valor(decimal.Round(quantia, 2) + 0.00m)); // sempre com duas casas: 30 vira 30.00

    public override string ToString() => $"{Quantia:0.00} {Moeda}";
}
