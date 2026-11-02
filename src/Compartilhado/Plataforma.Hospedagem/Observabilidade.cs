namespace Plataforma.Hospedagem;

/// <summary>
/// Logs estruturados e traces comuns a todos os serviços (spec observabilidade). Os logs saem em JSON, com o
/// identificador do trace e a correlação no escopo; os traces só são exportados quando há destino OTLP configurado.
/// </summary>
public static class Observabilidade
{
    /// <summary>Tag dos spans com o X-Correlation-Id, para achar o trace a partir da resposta ao cliente.</summary>
    public const string TagCorrelacao = "correlacao.id";

    /// <summary>Nome do campo de escopo dos logs com o X-Correlation-Id.</summary>
    public const string EscopoCorrelacao = "CorrelacaoId";

    /// <summary>Destino OTLP (variável padrão do OpenTelemetry). Ausente, nada é exportado.</summary>
    public const string ChaveDoDestino = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <param name="nomeDoServico">Nome com que o serviço aparece nos traces.</param>
    /// <param name="fontes">ActivitySources extras pelo nome, por exemplo "MassTransit" e "Npgsql".</param>
    public static TBuilder AdicionarObservabilidade<TBuilder>(this TBuilder builder, string nomeDoServico, params string[] fontes)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        var exportar = DeveExportar(builder.Configuration);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(nomeDoServico))
            .WithTracing(t =>
            {
                // As sondas de saúde chegam a cada poucos segundos e só gerariam ruído.
                t.AddAspNetCoreInstrumentation(o => o.Filter = contexto => !contexto.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));
                t.AddHttpClientInstrumentation();
                t.AddSource(fontes);
                if (exportar)
                {
                    t.AddOtlpExporter();
                }
            });

        return builder;
    }

    public static bool DeveExportar(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        return !string.IsNullOrWhiteSpace(configuracao[ChaveDoDestino]);
    }

    /// <summary>
    /// Marca a correlação no span atual e abre o escopo de log com ela. Usado nas requisições (UsarCorrelacao) e no
    /// consumo de eventos, em que a correlação vem da mensagem.
    /// </summary>
    public static IDisposable? MarcarCorrelacao(ILogger log, string correlacaoId)
    {
        ArgumentNullException.ThrowIfNull(log);

        Activity.Current?.SetTag(TagCorrelacao, correlacaoId);
        return log.BeginScope(new Dictionary<string, object> { [EscopoCorrelacao] = correlacaoId });
    }
}
