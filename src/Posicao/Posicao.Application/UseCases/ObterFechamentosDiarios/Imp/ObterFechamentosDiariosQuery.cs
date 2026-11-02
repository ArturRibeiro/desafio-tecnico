namespace Posicao.Application.UseCases.ObterFechamentosDiarios.Imp;

public sealed record ObterFechamentosDiariosQuery(TitularId Titular, ContaId ContaId, DateOnly? De, DateOnly? Ate, int Pagina, int TamanhoPagina) : IRequest<Resultado<FechamentosDiariosResponse>>;
