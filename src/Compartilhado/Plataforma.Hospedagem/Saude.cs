namespace Plataforma.Hospedagem;

/// <summary>
/// Endpoints de saúde comuns a todos os serviços (spec saude-dos-servicos).
/// </summary>
public static class Saude
{
    /// <summary>Tag das verificações executadas em /health/ready.</summary>
    public const string TagProntidao = "ready";

    public const string CaminhoVida = "/health/live";
    public const string CaminhoProntidao = "/health/ready";

    /// <summary>
    /// Mapeia /health/live (sem verificações, só indica que o processo responde) e
    /// /health/ready (verificações com a tag <see cref="TagProntidao"/>: 200 para Healthy ou Degraded, 503 para Unhealthy).
    /// </summary>
    public static IEndpointRouteBuilder MapearSaude(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(CaminhoVida, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscritorDeSaude.EscreverAsync,
        });

        endpoints.MapHealthChecks(CaminhoProntidao, new HealthCheckOptions
        {
            Predicate = registro => registro.Tags.Contains(TagProntidao),
            ResponseWriter = EscritorDeSaude.EscreverAsync,
        });

        return endpoints;
    }
}
