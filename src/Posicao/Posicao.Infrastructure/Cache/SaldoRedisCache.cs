namespace Posicao.Infrastructure.Cache;

/// <summary>
/// Saldos de abertura definitivos no Redis, com expiração de 24 h. Falhas do Redis viram um aviso no log e são
/// repassadas, para o caso de uso seguir pelo banco.
/// </summary>
internal sealed partial class SaldoRedisCache(IConnectionMultiplexer redis, ILogger<SaldoRedisCache> log) : ISaldoCache
{
    public const string Prefixo = "posicao:abertura:";
    private static readonly TimeSpan Expiracao = TimeSpan.FromHours(24);

    public async Task<decimal?> ObterAberturaAsync(ContaId contaId, DataFechamento data, CancellationToken cancelamento)
    {
        var valor = await RegistrandoFalhaAsync(() => redis.GetDatabase().StringGetAsync(Chave(contaId, data))).ConfigureAwait(false);
        return valor.HasValue ? decimal.Parse((string)valor!, CultureInfo.InvariantCulture) : null;
    }

    public Task GuardarAberturaAsync(ContaId contaId, DataFechamento data, decimal saldo, CancellationToken cancelamento) =>
        RegistrandoFalhaAsync(() => redis.GetDatabase().StringSetAsync(Chave(contaId, data), saldo.ToString(CultureInfo.InvariantCulture), Expiracao));

    /// <summary>Remove todas as aberturas guardadas, em todos os servidores do Redis.</summary>
    public async Task LimparAsync(CancellationToken cancelamento)
    {
        foreach (var servidor in redis.GetServers().Where(s => s.IsConnected && !s.IsReplica))
        {
            await foreach (var chave in servidor.KeysAsync(pattern: Prefixo + "*").WithCancellation(cancelamento).ConfigureAwait(false))
            {
                await redis.GetDatabase().KeyDeleteAsync(chave).ConfigureAwait(false);
            }
        }
    }

    private static string Chave(ContaId contaId, DataFechamento data) => $"{Prefixo}{contaId}:{data}";

    private async Task<T> RegistrandoFalhaAsync<T>(Func<Task<T>> operacao)
    {
        try
        {
            return await operacao().ConfigureAwait(false);
        }
        catch (Exception erro) when (erro is RedisException or TimeoutException)
        {
            CacheIndisponivel(erro);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache indisponível; a consulta segue pelo banco.")]
    private partial void CacheIndisponivel(Exception erro);
}
