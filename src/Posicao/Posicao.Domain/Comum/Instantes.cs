namespace Posicao.Domain.Comum;

public static class Instantes
{
    /// <summary>
    /// Converte para UTC e trunca em microssegundos, a precisão com que o banco guarda instantes.
    /// Assim, o mesmo instante lido do evento e relido do banco é idêntico.
    /// </summary>
    public static DateTimeOffset Normalizar(DateTimeOffset instante)
    {
        var utc = instante.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
