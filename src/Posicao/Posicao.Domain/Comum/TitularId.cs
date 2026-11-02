namespace Posicao.Domain.Comum;

/// <summary>Titular da conta: o identificador opaco do cliente no Provedor de Identidade (claim "sub").</summary>
public readonly record struct TitularId
{
    public const int TamanhoMaximo = 200;

    private TitularId(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static Resultado<TitularId> Criar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Length > TamanhoMaximo
            ? Resultado.Falhar<TitularId>(CodigosDeFalha.TitularInvalido, "Titular ausente ou maior que 200 caracteres.")
            : Resultado.Ok(new TitularId(valor));

    public override string ToString() => Valor;
}
