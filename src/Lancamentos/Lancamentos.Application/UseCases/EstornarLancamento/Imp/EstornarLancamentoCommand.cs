namespace Lancamentos.Application.UseCases.EstornarLancamento.Imp;

public sealed record EstornarLancamentoCommand(
    TitularId Titular,
    ContaId ContaId,
    LancamentoId LancamentoId,
    Descricao? Descricao,
    ChaveIdempotencia Chave) : IRequest<Resultado<LancamentoResponse>>;
