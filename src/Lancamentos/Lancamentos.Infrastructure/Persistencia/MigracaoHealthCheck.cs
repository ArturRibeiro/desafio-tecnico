namespace Lancamentos.Infrastructure.Persistencia;

/// <summary>Unhealthy enquanto houver migração pendente ou o banco estiver inacessível.</summary>
internal sealed class MigracaoHealthCheck(IServiceScopeFactory escopos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var pendentes = await escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>().Database
                .GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            return pendentes.Any() ? HealthCheckResult.Unhealthy("Migrações pendentes.") : HealthCheckResult.Healthy();
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Não foi possível verificar as migrações.", erro);
        }
    }
}
