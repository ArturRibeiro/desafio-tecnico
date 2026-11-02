namespace Posicao.Infrastructure.Persistencia;

/// <summary>
/// Aplica as migrações em segundo plano e tenta de novo até o banco aceitar conexões. O processo nunca encerra
/// por isso: enquanto houver migração pendente, a prontidão fica 503 pela verificação "migracoes".
/// O EF Core obtém um lock no banco durante a migração, então várias instâncias não migram ao mesmo tempo.
/// </summary>
internal sealed partial class AplicadorDeMigracoes(IServiceScopeFactory escopos, IOptions<OpcoesDeBanco> opcoes, ILogger<AplicadorDeMigracoes> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.AplicarMigracoesNaSubida)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<PosicaoDbContext>().Database.MigrateAsync(stoppingToken).ConfigureAwait(false);
                MigracoesAplicadas();
                return;
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                FalhaAoMigrar(erro, opcoes.Value.IntervaloEntreTentativas);
                await Task.Delay(opcoes.Value.IntervaloEntreTentativas, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrações do banco de Posição aplicadas.")]
    private partial void MigracoesAplicadas();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao aplicar as migrações; nova tentativa em {Intervalo}.")]
    private partial void FalhaAoMigrar(Exception erro, TimeSpan intervalo);
}
