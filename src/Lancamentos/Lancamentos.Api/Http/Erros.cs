namespace Lancamentos.Api.Http;

/// <summary>Falha de negócio → status HTTP + Problem Details com o campo "codigo" (DOMINIO.md, seção 5.4).</summary>
internal static class Erros
{
    public const string RequisicaoInvalida = "requisicao-invalida";

    private static readonly Dictionary<string, int> Status = new(StringComparer.Ordinal)
    {
        [CodigosDeFalha.ValorInvalido] = StatusCodes.Status400BadRequest,
        [CodigosDeFalha.DescricaoInvalida] = StatusCodes.Status400BadRequest,
        [CodigosDeFalha.ChaveIdempotenciaObrigatoria] = StatusCodes.Status400BadRequest,
        [CodigosDeFalha.ChaveIdempotenciaInvalida] = StatusCodes.Status400BadRequest,
        [CodigosDeFalha.ContaNaoEncontrada] = StatusCodes.Status404NotFound,
        [CodigosDeFalha.LancamentoNaoEncontrado] = StatusCodes.Status404NotFound,
        [CodigosDeFalha.LancamentoJaEstornado] = StatusCodes.Status409Conflict,
        [CodigosDeFalha.ChaveIdempotenciaConflitante] = StatusCodes.Status409Conflict,
        [CodigosDeFalha.SaldoInsuficiente] = StatusCodes.Status422UnprocessableEntity,
        [CodigosDeFalha.ContaInativa] = StatusCodes.Status422UnprocessableEntity,
        [CodigosDeFalha.DataCompetenciaInvalida] = StatusCodes.Status422UnprocessableEntity,
        [CodigosDeFalha.EstornoNaoEstornavel] = StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>Código das respostas de erro que não vêm de uma falha de negócio.</summary>
    public static string CodigoPadrao(int status) => status switch
    {
        StatusCodes.Status400BadRequest => RequisicaoInvalida,
        StatusCodes.Status401Unauthorized => "nao-autenticado",
        StatusCodes.Status403Forbidden => "sem-permissao",
        StatusCodes.Status404NotFound => "nao-encontrado",
        StatusCodes.Status405MethodNotAllowed => "metodo-nao-permitido",
        _ => "erro-interno",
    };

    public static IResult Problema(Falha falha) =>
        Results.Problem(
            statusCode: Status.GetValueOrDefault(falha.Codigo, StatusCodes.Status422UnprocessableEntity),
            title: falha.Mensagem,
            extensions: new Dictionary<string, object?> { ["codigo"] = falha.Codigo });
}
