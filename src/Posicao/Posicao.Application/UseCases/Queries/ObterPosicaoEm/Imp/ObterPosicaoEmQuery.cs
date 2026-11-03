namespace Posicao.Application.UseCases.Queries.ObterPosicaoEm.Imp;

public sealed record ObterPosicaoEmQuery(TitularId Titular, ContaId ContaId, DateTimeOffset? Instante) : IRequest<Resultado<PosicaoConsolidadaResponse>>;
