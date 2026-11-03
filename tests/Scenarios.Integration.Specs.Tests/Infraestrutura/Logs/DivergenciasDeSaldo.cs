namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Logs;

/// <summary>
/// Captura, no Posicao.Worker do cenário, os logs de conciliação com o código "saldo-divergente": é o registro "para
/// análise" que a spec projecao-da-posicao exige. Sobrevive às paradas do Worker, porque pertence ao cenário.
/// </summary>
public sealed class DivergenciasDeSaldo : ILoggerProvider
{
    private const string Codigo = "saldo-divergente";

    private readonly ConcurrentQueue<Registrada> registradas = new();

    public IReadOnlyList<Registrada> Registradas => [.. registradas];

    public void Limpar() => registradas.Clear();

    public ILogger CreateLogger(string categoryName) => new Captura(registradas);

    public void Dispose()
    {
        // Nada a liberar: a fila pertence ao cenário.
    }

    /// <summary>Divergência como o log a identifica: conta, sequência e lançamento.</summary>
    public sealed record Registrada(string ContaId, long Sequencia, string LancamentoId);

    private sealed class Captura(ConcurrentQueue<Registrada> registradas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || state is not IReadOnlyList<KeyValuePair<string, object?>> campos)
            {
                return;
            }

            var valores = campos.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
            if (valores.TryGetValue("Codigo", out var codigo) && Codigo.Equals(codigo as string, StringComparison.Ordinal))
            {
                registradas.Enqueue(new Registrada(
                    Convert.ToString(valores["ContaId"], CultureInfo.InvariantCulture)!,
                    Convert.ToInt64(valores["Sequencia"], CultureInfo.InvariantCulture),
                    Convert.ToString(valores["LancamentoId"], CultureInfo.InvariantCulture)!));
            }
        }
    }
}
