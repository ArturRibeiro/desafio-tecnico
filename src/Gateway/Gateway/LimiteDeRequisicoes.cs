namespace Gateway;

/// <summary>
/// Limite de requisições por cliente (spec api-gateway). O cliente é o titular de um token válido; sem token válido,
/// é o endereço de origem da conexão. O X-Forwarded-For não é usado, porque o cliente o forja. O limite vale por
/// instância do gateway e só para as rotas do proxy, então a saúde do gateway fica fora dele.
/// </summary>
internal static class LimiteDeRequisicoes
{
    public const string Politica = "por-cliente";
    public const string Codigo = "limite-excedido";

    public static IServiceCollection AdicionarLimiteDeRequisicoes(this IServiceCollection servicos, IConfiguration secao)
    {
        var opcoes = secao.Get<LimiteOptions>() ?? new LimiteOptions();

        return servicos.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Politica, contexto => RateLimitPartition.GetFixedWindowLimiter(
                Cliente(contexto),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opcoes.Permissoes,
                    Window = opcoes.Janela,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
            o.OnRejected = (rejeicao, cancelamento) => new ValueTask(RecusarAsync(rejeicao, opcoes.Janela));
        });
    }

    /// <summary>"titular:{sub}" para um token válido (UseAuthentication já o validou); senão "ip:{origem}".</summary>
    public static string Cliente(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.User.Identity?.IsAuthenticated == true && Autenticacao.ObterTitular(contexto.User) is { } titular
            ? $"titular:{titular}"
            : $"ip:{contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido"}";
    }

    private static Task RecusarAsync(OnRejectedContext rejeicao, TimeSpan janela)
    {
        var espera = rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var restante) ? restante : janela;
        var segundos = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds));
        var http = rejeicao.HttpContext;
        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

        var extensoes = new Dictionary<string, object?> { ["codigo"] = Codigo };
        if (http.Request.Headers[Correlacao.Cabecalho].ToString() is { Length: > 0 } correlacao)
        {
            extensoes["correlacaoId"] = correlacao;
        }

        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Limite de requisições excedido. Tente de novo depois do tempo indicado em Retry-After.",
            extensions: extensoes).ExecuteAsync(http);
    }
}
