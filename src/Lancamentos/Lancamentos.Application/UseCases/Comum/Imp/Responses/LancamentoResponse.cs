namespace Lancamentos.Application.UseCases.Comum.Imp.Responses;

/// <summary>Crédito, débito ou estorno registrado, como a API o devolve.</summary>
public sealed record LancamentoResponse(
    Guid LancamentoId,
    Guid ContaId,
    string Tipo,
    decimal Valor,
    string Moeda,
    string Descricao,
    DateTimeOffset DataCompetencia,
    DateTimeOffset DataRegistro,
    long Sequencia,
    Guid? LancamentoEstornadoId,
    decimal SaldoDisponivel)
{
    public static LancamentoResponse De(Lancamento lancamento)
    {
        ArgumentNullException.ThrowIfNull(lancamento);
        return new(
            lancamento.Id.Valor,
            lancamento.ContaId.Valor,
            lancamento.Tipo.ToString(),
            Dinheiro.DuasCasas(lancamento.Valor.Quantia),
            Domain.Comum.Valor.Moeda,
            lancamento.Descricao.Valor,
            lancamento.DataCompetencia,
            lancamento.DataRegistro,
            lancamento.Sequencia,
            lancamento.LancamentoEstornadoId?.Valor,
            Dinheiro.DuasCasas(lancamento.SaldoDisponivelApos));
    }
}
