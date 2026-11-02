namespace Lancamentos.Domain.Comum;

/// <summary>Códigos estáveis das falhas de negócio de Lançamentos (DOMINIO.md, seção 5.4).</summary>
public static class CodigosDeFalha
{
    public const string ContaNaoEncontrada = "conta-nao-encontrada";
    public const string ContaInativa = "conta-inativa";
    public const string SaldoInsuficiente = "saldo-insuficiente";
    public const string ValorInvalido = "valor-invalido";
    public const string DescricaoInvalida = "descricao-invalida";
    public const string DataCompetenciaInvalida = "data-competencia-invalida";
    public const string TitularInvalido = "titular-invalido";
    public const string ChaveIdempotenciaObrigatoria = "chave-idempotencia-obrigatoria";
    public const string ChaveIdempotenciaInvalida = "chave-idempotencia-invalida";
    public const string ChaveIdempotenciaConflitante = "chave-idempotencia-conflitante";
    public const string LancamentoNaoEncontrado = "lancamento-nao-encontrado";
    public const string LancamentoJaEstornado = "lancamento-ja-estornado";
    public const string EstornoNaoEstornavel = "estorno-nao-estornavel";
}
