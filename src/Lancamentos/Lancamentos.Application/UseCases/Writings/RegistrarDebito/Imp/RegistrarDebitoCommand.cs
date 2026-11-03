namespace Lancamentos.Application.UseCases.Writings.RegistrarDebito.Imp;

public sealed record RegistrarDebitoCommand(
    TitularId Titular,
    ContaId ContaId,
    Valor Valor,
    Descricao Descricao,
    DateTimeOffset? DataCompetencia,
    ChaveIdempotencia Chave)
    : RegistrarLancamentoCommand(Titular, ContaId, Valor, Descricao, DataCompetencia, Chave), IRequest<Resultado<LancamentoResponse>>;
