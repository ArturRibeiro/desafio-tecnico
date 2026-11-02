namespace Lancamentos.Domain.Comum;

/// <summary>Descrição do lançamento: obrigatória, de 1 a 200 caracteres sem contar espaços nas pontas.</summary>
public readonly record struct Descricao
{
    public const int TamanhoMaximo = 200;

    private Descricao(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static Resultado<Descricao> Criar(string? valor)
    {
        var aparada = valor?.Trim();
        return string.IsNullOrEmpty(aparada) || aparada.Length > TamanhoMaximo
            ? Resultado.Falhar<Descricao>(CodigosDeFalha.DescricaoInvalida, "A descrição é obrigatória e deve ter até 200 caracteres.")
            : Resultado.Ok(new Descricao(aparada));
    }

    public override string ToString() => Valor;
}
