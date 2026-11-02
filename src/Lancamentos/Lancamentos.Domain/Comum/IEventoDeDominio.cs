namespace Lancamentos.Domain.Comum;

/// <summary>
/// Fato ocorrido dentro do contexto de Lançamentos. Não sai do contexto: a Infraestrutura o traduz
/// para um Integration Event (Published Language) gravado no Outbox.
/// </summary>
public interface IEventoDeDominio
{
    /// <summary>Identificador único do fato; vira o eventoId do Integration Event.</summary>
    Guid EventoId { get; }
}
