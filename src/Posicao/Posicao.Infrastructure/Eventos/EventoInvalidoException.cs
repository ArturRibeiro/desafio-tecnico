namespace Posicao.Infrastructure.Eventos;

/// <summary>
/// Evento que nunca seria processado, por mais que fosse tentado de novo. A retentativa o ignora, e ele vai direto
/// para a fila de falhas (_error), sem bloquear os demais.
/// </summary>
public sealed class EventoInvalidoException : Exception
{
    public EventoInvalidoException()
    {
    }

    public EventoInvalidoException(string message)
        : base(message)
    {
    }

    public EventoInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
