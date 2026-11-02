namespace Lancamentos.Application.UseCases.AbrirConta.Imp.Responses;

/// <summary>Conta aberta, como a API a devolve.</summary>
public sealed record ContaResponse(Guid ContaId, string Situacao, string Moeda, decimal SaldoDisponivel, decimal Limite, DateTimeOffset DataAbertura)
{
    public static ContaResponse De(Conta conta)
    {
        ArgumentNullException.ThrowIfNull(conta);
        return new(conta.Id.Valor, conta.Situacao.ToString(), Valor.Moeda, Dinheiro.DuasCasas(conta.SaldoDisponivel), Dinheiro.DuasCasas(conta.Limite), conta.DataAbertura);
    }
}
