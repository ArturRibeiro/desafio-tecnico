namespace Posicao.Application.UseCases.Writings.ProjetarAbertura.Imp;

public sealed record ProjetarAberturaCommand(ContaId ContaId, TitularId Titular, DateTimeOffset DataAbertura) : IRequest<ResumoDaProjecao>;
