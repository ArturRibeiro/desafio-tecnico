namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Cenario;

/// <summary>Espera por condição, nunca por intervalo fixo: a Posição é atualizada com consistência eventual.</summary>
public static class Esperar
{
    public static readonly TimeSpan Prazo = TimeSpan.FromSeconds(60);

    public static async Task AteAsync(Func<Task<bool>> condicao, string descricao, TimeSpan? prazo = null)
    {
        ArgumentNullException.ThrowIfNull(condicao);

        var limite = DateTime.UtcNow + (prazo ?? Prazo);
        while (DateTime.UtcNow < limite)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Esperado, e não aconteceu em {(prazo ?? Prazo).TotalSeconds} s: {descricao}.");
    }

    /// <summary>A condição deve continuar falsa durante todo o intervalo (por exemplo, "nada foi publicado").</summary>
    public static async Task NuncaAsync(Func<Task<bool>> condicao, string descricao, TimeSpan intervalo)
    {
        ArgumentNullException.ThrowIfNull(condicao);

        var limite = DateTime.UtcNow + intervalo;
        while (DateTime.UtcNow < limite)
        {
            if (await condicao())
            {
                throw new InvalidOperationException($"Não deveria acontecer: {descricao}.");
            }

            await Task.Delay(200);
        }
    }
}
