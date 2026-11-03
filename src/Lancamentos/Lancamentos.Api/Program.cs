var builder = WebApplication.CreateBuilder(args);
builder.AdicionarObservabilidade("lancamentos-api", "MassTransit", "Npgsql");

var postgres = builder.Configuration.ConnectionStringObrigatoria("Postgres");
builder.Services.AdicionarInfraestruturaDeLancamentos(postgres, builder.Configuration.GetSection("Banco"));
builder.Services.AdicionarMediator(typeof(AbrirContaCommand).Assembly);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContextoDaRequisicao, ContextoDaRequisicao>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AdicionarAutenticacaoJwt(builder.Configuration.GetSection("Autenticacao"));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new InstanteComFusoJsonConverter()));

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

builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgres", tags: [Saude.TagProntidao], timeout: TimeSpan.FromSeconds(3))
    .AdicionarMigracaoHealthCheck(Saude.TagProntidao);

// O MassTransit conecta ao broker em segundo plano: a API sobe e aceita lançamentos mesmo com o RabbitMQ fora,
// e as mensagens esperam no Outbox.
var rabbitMq = new Uri(builder.Configuration.ConnectionStringObrigatoria("RabbitMq"));
builder.Services.AddMassTransit(x =>
{
    x.ConfigureHealthCheckOptions(o =>
    {
        o.Name = "rabbitmq";
        o.Tags.Add(Saude.TagProntidao);
    });
    x.AdicionarOutboxDeLancamentos();
    x.UsingRabbitMq((contexto, cfg) =>
    {
        cfg.Host(rabbitMq);
        cfg.ConfigureEndpoints(contexto);
    });
});

var app = builder.Build();

app.UsarCorrelacao();
// Corpo malformado vira BadHttpRequestException (lançada em Development): mantém o 400 dela; o resto é 500.
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
app.MapearLancamentos();

await app.RunAsync();
