namespace Posicao.Application.UseCases.ObterFechamentosDiarios.Imp.Responses;

/// <summary>Fechamento de um dia com movimento, como a API o devolve.</summary>
public sealed record FechamentoDiarioResponse(string Data, decimal SaldoAbertura, decimal TotalCreditos, decimal TotalDebitos, decimal SaldoFechamento)
{
    public static FechamentoDiarioResponse De(FechamentoDiario fechamento)
    {
        ArgumentNullException.ThrowIfNull(fechamento);
        return new(
            fechamento.Data.ToString(),
            Dinheiro.DuasCasas(fechamento.SaldoAbertura),
            Dinheiro.DuasCasas(fechamento.TotalCreditos),
            Dinheiro.DuasCasas(fechamento.TotalDebitos),
            Dinheiro.DuasCasas(fechamento.SaldoFechamento));
    }
}
