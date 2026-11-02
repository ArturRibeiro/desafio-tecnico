namespace Posicao.Application.UseCases.ProjetarAbertura.Imp;

public sealed record ProjetarAberturaCommand(ContaId ContaId, TitularId Titular, DateTimeOffset DataAbertura) : IRequest<ResumoDaProjecao>;
