namespace Posicao.Api.Http;

/// <summary>
/// Consultas de Posição Consolidada. Todas exigem um token com o escopo posicao:ler; o titular vem sempre do "sub"
/// do token, e só as contas dele são consultadas.
/// </summary>
internal static class Endpoints
{
    public const string EscopoDeLeitura = "posicao:ler";

    public static void MapearPosicao(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/posicao").ExigirEscopo(EscopoDeLeitura);

        grupo.MapGet("/contas/{contaId:guid}", ObterPosicaoAsync);
        grupo.MapGet("/contas/{contaId:guid}/fechamentos", ObterFechamentosAsync);
    }

    private static async Task<IResult> ObterPosicaoAsync(Guid contaId, string? instante, HttpContext http, IMediator mediator)
    {
        var lido = Parametros.Instante(instante);
        if (lido.Falha is { } falha)
        {
            return Erros.Problema(falha);
        }

        var resultado = await mediator.SendAsync(new ObterPosicaoEmQuery(Titular(http), new ContaId(contaId), lido.Valor), http.RequestAborted).ConfigureAwait(false);
        return resultado.Falha is { } falhaNoCaso
            ? Erros.Problema(falhaNoCaso)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObterFechamentosAsync(
        Guid contaId,
        string? de,
        string? ate,
        string? pagina,
        string? tamanhoPagina,
        HttpContext http,
        IMediator mediator)
    {
        var consulta = new ObterFechamentosDiariosQuery(
            Titular(http),
            new ContaId(contaId),
            Parametros.Data(de),
            Parametros.Data(ate),
            Parametros.Inteiro(pagina, 1),
            Parametros.Inteiro(tamanhoPagina, 30));
        var resultado = await mediator.SendAsync(consulta, http.RequestAborted).ConfigureAwait(false);
        return resultado.Falha is { } falha
            ? Erros.Problema(falha)
            : Results.Ok(resultado.Valor);
    }

    /// <summary>O "sub" já foi validado na autenticação (spec autenticacao): aqui ele sempre existe e é válido.</summary>
    private static TitularId Titular(HttpContext http) => TitularId.Criar(Autenticacao.ObterTitular(http.User)).Valor;
}
