namespace Lancamentos.Domain.Comum;

public sealed record LancamentoRegistrado(Guid EventoId, Lancamento Lancamento) : IEventoDeDominio;
