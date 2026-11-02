namespace Lancamentos.Domain.Comum;

public sealed record LancamentoEstornado(Guid EventoId, Lancamento Estorno) : IEventoDeDominio;
