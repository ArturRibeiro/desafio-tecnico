namespace Plataforma.Hospedagem;

/// <summary>
/// Garante um X-Correlation-Id em toda requisição e o devolve na resposta (spec api-gateway).
/// Um valor fora do formato é descartado e substituído, para não permitir injeção em logs.
/// O valor também vai para o escopo dos logs e para o span da requisição (spec observabilidade).
/// </summary>
public static partial class Correlacao
{
    public const string Cabecalho = "X-Correlation-Id";

    public static IApplicationBuilder UsarCorrelacao(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (contexto, proximo) =>
        {
            // Vários valores no cabeçalho chegam unidos por vírgula e também são rejeitados.
            var recebido = contexto.Request.Headers[Cabecalho].ToString();
            var valor = FormatoValido().IsMatch(recebido) ? recebido : Guid.NewGuid().ToString();

            contexto.Request.Headers[Cabecalho] = valor;
            contexto.Response.OnStarting(() =>
            {
                contexto.Response.Headers[Cabecalho] = valor;
                return Task.CompletedTask;
            });

            var log = contexto.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Correlacao).FullName!);
            using var escopo = Observabilidade.MarcarCorrelacao(log, valor);
            await proximo(contexto).ConfigureAwait(false);
        });
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex FormatoValido();
}
