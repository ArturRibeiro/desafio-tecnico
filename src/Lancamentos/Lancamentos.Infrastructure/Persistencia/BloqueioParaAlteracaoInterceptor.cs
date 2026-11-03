namespace Lancamentos.Infrastructure.Persistencia;

/// <summary>
/// O EF não tem <c>FOR UPDATE</c> em LINQ. Uma consulta marcada com <see cref="Tag"/> (<c>TagWith</c>) recebe
/// <c>FOR UPDATE</c> no fim do SQL gerado: a linha fica bloqueada até o fim da transação. Só o Repository marca
/// consultas; o restante do SQL continua sendo o que o EF gera a partir do LINQ.
/// </summary>
internal sealed class BloqueioParaAlteracaoInterceptor : DbCommandInterceptor
{
    public const string Tag = "bloqueio-para-alteracao";

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Bloquear(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Bloquear(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void Bloquear(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // O EF escreve a tag como comentário SQL: "-- bloqueio-para-alteracao". Valores nunca entram no texto do
        // comando (são parâmetros), então só uma consulta marcada pelo Repository tem esse comentário.
        if (command.CommandText.Contains($"-- {Tag}", StringComparison.Ordinal))
        {
            command.CommandText += " FOR UPDATE";
        }
    }
}
