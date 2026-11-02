var builder = WebApplication.CreateBuilder(args);
builder.AdicionarObservabilidade("posicao-api", "Npgsql");

var postgres = builder.Configuration.ConnectionStringObrigatoria("Postgres");
var redis = builder.Configuration.ConnectionStringObrigatoria("Redis");
builder.Services.AdicionarInfraestruturaDePosicao(postgres, builder.Configuration.GetSection("Banco"));
builder.Services.AdicionarMediator(typeof(ObterPosicaoEmQuery).Assembly);
builder.Services.AdicionarCacheDeSaldos(redis);
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration.GetSection("Autenticacao"));

// Toda resposta de erro em Problem Details com "codigo"; nenhuma expõe stack trace, SQL ou mensagem de exceção.
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = contexto =>
{
    var status = contexto.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
    contexto.ProblemDetails.Extensions.TryAdd("codigo", Erros.CodigoPadrao(status));
    contexto.ProblemDetails.Extensions.Remove("exception");
    contexto.ProblemDetails.Detail = status >= StatusCodes.Status500InternalServerError ? null : contexto.ProblemDetails.Detail;
    if (contexto.HttpContext.Request.Headers[Correlacao.Cabecalho].ToString() is { Length: > 0 } correlacao)
    {
        contexto.ProblemDetails.Extensions["correlacaoId"] = correlacao;
    }
});

// As migrações são aplicadas pelo Posicao.Worker; a Api só verifica se o esquema está atualizado.
builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgres", tags: [Saude.TagProntidao], timeout: TimeSpan.FromSeconds(3))
    .AdicionarVerificacaoDeMigracoes(Saude.TagProntidao)
    // O cache é opcional: fora do ar, as consultas vão direto ao banco. Por isso Degraded, que mantém o 200.
    .AddRedis(redis, name: "redis", failureStatus: HealthStatus.Degraded, tags: [Saude.TagProntidao], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

app.UsarCorrelacao();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = erro => erro is BadHttpRequestException requisicaoInvalida
        ? requisicaoInvalida.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapearSaude();
app.MapearPosicao();

await app.RunAsync();
