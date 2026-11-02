namespace Lancamentos.Api.Http;

/// <summary>
/// Rotas de Lançamentos. Todas exigem um token com o escopo lancamentos:escrever; o titular vem sempre do "sub"
/// do token. A entrada é validada aqui (400) antes de chegar ao caso de uso, que aplica titularidade,
/// idempotência e regras de negócio, nessa ordem.
/// </summary>
internal static class Endpoints
{
    public const string EscopoDeEscrita = "lancamentos:escrever";
    private const string CabecalhoDeIdempotencia = "Idempotency-Key";

    public static void MapearLancamentos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/lancamentos").ExigirEscopo(EscopoDeEscrita);

        grupo.MapPost("/contas", AbrirContaAsync);
        grupo.MapPost("/contas/{contaId:guid}/creditos", (Guid contaId, LancamentoRequisicao corpo, HttpContext http, IMediator mediator) =>
            RegistrarAsync(contaId, corpo, http, mediator, (titular, conta, valor, descricao, competencia, chave) =>
                new RegistrarCreditoCommand(titular, conta, valor, descricao, competencia, chave)));
        grupo.MapPost("/contas/{contaId:guid}/debitos", (Guid contaId, LancamentoRequisicao corpo, HttpContext http, IMediator mediator) =>
            RegistrarAsync(contaId, corpo, http, mediator, (titular, conta, valor, descricao, competencia, chave) =>
                new RegistrarDebitoCommand(titular, conta, valor, descricao, competencia, chave)));
        grupo.MapPost("/contas/{contaId:guid}/lancamentos/{lancamentoId:guid}/estornos", EstornarAsync);
    }

    private static async Task<IResult> AbrirContaAsync(HttpContext http, IMediator mediator)
    {
        var chave = ChaveIdempotencia.Criar(http.Request.Headers[CabecalhoDeIdempotencia]);
        if (chave.Falha is { } falha)
        {
            return Erros.Problema(falha);
        }

        var resultado = await mediator.SendAsync(new AbrirContaCommand(Titular(http), chave.Valor), http.RequestAborted).ConfigureAwait(false);
        return resultado.Falha is { } falhaNoCaso
            ? Erros.Problema(falhaNoCaso)
            : Results.Created($"/lancamentos/contas/{resultado.Valor.ContaId}", resultado.Valor);
    }

    private static async Task<IResult> RegistrarAsync(
        Guid contaId,
        LancamentoRequisicao corpo,
        HttpContext http,
        IMediator mediator,
        Func<TitularId, ContaId, Valor, Descricao, DateTimeOffset?, ChaveIdempotencia, IRequest<Resultado<LancamentoResponse>>> criarComando)
    {
        var chave = ChaveIdempotencia.Criar(http.Request.Headers[CabecalhoDeIdempotencia]);
        var valor = Valor.Criar(corpo.Valor ?? 0m);
        var descricao = Descricao.Criar(corpo.Descricao);
        if ((chave.Falha ?? valor.Falha ?? descricao.Falha) is { } falha)
        {
            return Erros.Problema(falha);
        }

        var comando = criarComando(Titular(http), new ContaId(contaId), valor.Valor, descricao.Valor, corpo.DataCompetencia, chave.Valor);
        return Responder(await mediator.SendAsync(comando, http.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<IResult> EstornarAsync(
        Guid contaId,
        Guid lancamentoId,
        [FromBody] EstornoRequisicao? corpo,
        HttpContext http,
        IMediator mediator)
    {
        var chave = ChaveIdempotencia.Criar(http.Request.Headers[CabecalhoDeIdempotencia]);
        Resultado<Descricao>? descricao = corpo?.Descricao is null ? null : Descricao.Criar(corpo.Descricao);
        if ((chave.Falha ?? descricao?.Falha) is { } falha)
        {
            return Erros.Problema(falha);
        }

        var comando = new EstornarLancamentoCommand(Titular(http), new ContaId(contaId), new LancamentoId(lancamentoId), descricao?.Valor, chave.Valor);
        return Responder(await mediator.SendAsync(comando, http.RequestAborted).ConfigureAwait(false));
    }

    private static IResult Responder(Resultado<LancamentoResponse> resultado) =>
        resultado.Falha is { } falha
            ? Erros.Problema(falha)
            : Results.Json(resultado.Valor, statusCode: StatusCodes.Status201Created);

    /// <summary>O token já foi validado, inclusive o "sub" (ver AdicionarAutenticacaoJwt).</summary>
    private static TitularId Titular(HttpContext http) => TitularId.Criar(Autenticacao.ObterTitular(http.User)).Valor;
}
