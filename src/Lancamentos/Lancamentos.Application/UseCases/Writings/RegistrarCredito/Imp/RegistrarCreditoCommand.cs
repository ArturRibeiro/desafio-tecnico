namespace Lancamentos.Application.UseCases.Writings.RegistrarCredito.Imp;

public sealed record RegistrarCreditoCommand(
    TitularId Titular,
    ContaId ContaId,
    Valor Valor,
    Descricao Descricao,
    DateTimeOffset? DataCompetencia,
    ChaveIdempotencia Chave)
    : RegistrarLancamentoCommand(Titular, ContaId, Valor, Descricao, DataCompetencia, Chave), IRequest<Resultado<LancamentoResponse>>;
