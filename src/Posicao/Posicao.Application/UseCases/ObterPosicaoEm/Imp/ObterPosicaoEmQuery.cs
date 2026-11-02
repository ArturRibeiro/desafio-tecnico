namespace Posicao.Application.UseCases.ObterPosicaoEm.Imp;

public sealed record ObterPosicaoEmQuery(TitularId Titular, ContaId ContaId, DateTimeOffset? Instante) : IRequest<Resultado<PosicaoConsolidadaResponse>>;
