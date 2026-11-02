namespace Posicao.Application.UseCases.Comum.Imp;

internal static class Cache
{
    /// <summary>O cache é opcional: qualquer falha dele vira "sem valor", e a consulta segue pelo banco.</summary>
    public static async Task<decimal?> TentarAsync(Func<Task<decimal?>> operacao)
    {
        try
        {
            return await operacao().ConfigureAwait(false);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            return null;
        }
    }
}
