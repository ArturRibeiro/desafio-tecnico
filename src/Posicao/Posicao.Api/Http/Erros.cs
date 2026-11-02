namespace Posicao.Api.Http;

/// <summary>Falha de negócio → status HTTP + Problem Details com o campo "codigo" (DOMINIO.md, seção 6.5).</summary>
internal static class Erros
{
    public const string RequisicaoInvalida = "requisicao-invalida";

    private static readonly Dictionary<string, int> Status = new(StringComparer.Ordinal)
    {
        [CodigosDeFalha.ContaNaoEncontrada] = StatusCodes.Status404NotFound,
        [CodigosDeFalha.InstanteInvalido] = StatusCodes.Status400BadRequest,
        [CodigosDeFalha.PeriodoInvalido] = StatusCodes.Status400BadRequest,
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
            statusCode: Status.GetValueOrDefault(falha.Codigo, StatusCodes.Status400BadRequest),
            title: falha.Mensagem,
            extensions: new Dictionary<string, object?> { ["codigo"] = falha.Codigo });
}
