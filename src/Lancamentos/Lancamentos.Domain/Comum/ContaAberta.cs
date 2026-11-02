namespace Lancamentos.Domain.Comum;

public sealed record ContaAberta(Guid EventoId, Conta Conta) : IEventoDeDominio;
