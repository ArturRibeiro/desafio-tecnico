namespace Posicao.Domain.Comum;

/// <summary>Códigos estáveis das falhas de Posição Consolidada (DOMINIO.md, seção 6.5).</summary>
public static class CodigosDeFalha
{
    public const string ContaNaoEncontrada = "conta-nao-encontrada";
    public const string InstanteInvalido = "instante-invalido";
    public const string PeriodoInvalido = "periodo-invalido";

    public const string ValorInvalido = "valor-invalido";
    public const string TitularInvalido = "titular-invalido";
    public const string MovimentoInvalido = "movimento-invalido";

    /// <summary>Movimento com competência num dia anterior ao último fechamento da conta (DOMINIO.md, seção 6.1).</summary>
    public const string CompetenciaForaDeOrdem = "competencia-fora-de-ordem";

    /// <summary>Estorno cujo lançamento original não foi recebido pela Posição.</summary>
    public const string EstornoSemOriginal = "estorno-sem-original";
}
