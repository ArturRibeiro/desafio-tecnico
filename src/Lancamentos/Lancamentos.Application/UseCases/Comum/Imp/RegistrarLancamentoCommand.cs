namespace Lancamentos.Application.UseCases.Comum.Imp;

/// <summary>Dados comuns a um crédito e a um débito. Cada um tem o seu pedido, porque cada pedido tem um único handler.</summary>
public abstract record RegistrarLancamentoCommand(
    TitularId Titular,
    ContaId ContaId,
    Valor Valor,
    Descricao Descricao,
    DateTimeOffset? DataCompetencia,
    ChaveIdempotencia Chave);
