namespace Lancamentos.Domain.Comum;

/// <summary>Chave de idempotência enviada pelo cliente: 1 a 100 caracteres ASCII visíveis.</summary>
public readonly record struct ChaveIdempotencia
{
    public const int TamanhoMaximo = 100;

    private ChaveIdempotencia(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static Resultado<ChaveIdempotencia> Criar(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return Resultado.Falhar<ChaveIdempotencia>(CodigosDeFalha.ChaveIdempotenciaObrigatoria, "O cabeçalho Idempotency-Key é obrigatório.");
        }

        return valor.Length > TamanhoMaximo || valor.Any(c => c is < '!' or > '~')
            ? Resultado.Falhar<ChaveIdempotencia>(CodigosDeFalha.ChaveIdempotenciaInvalida, "A Idempotency-Key deve ter de 1 a 100 caracteres ASCII visíveis.")
            : Resultado.Ok(new ChaveIdempotencia(valor));
    }

    public override string ToString() => Valor;
}
