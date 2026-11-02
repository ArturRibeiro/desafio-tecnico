namespace Lancamentos.Unit.Tests.Dominio;

/// <summary>Valores válidos de uso comum nos testes, com um instante fixo.</summary>
internal static class Fabrica
{
    public static readonly DateTimeOffset Agora = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    public static readonly TitularId Titular = TitularId.Criar("cliente-a").Valor;

    public static Valor Valor(decimal quantia) => Lancamentos.Domain.Comum.Valor.Criar(quantia).Valor;

    public static Descricao Descricao(string texto = "Pix") => Lancamentos.Domain.Comum.Descricao.Criar(texto).Valor;

    public static ChaveIdempotencia Chave(string? texto = null) => ChaveIdempotencia.Criar(texto ?? Guid.NewGuid().ToString()).Valor;

    public static Conta ContaComSaldo(decimal saldo)
    {
        var conta = Conta.Abrir(ContaId.Novo(), Titular, Chave(), Agora);
        if (saldo > 0)
        {
            conta.RegistrarCredito(Valor(saldo), Descricao(), null, Chave(), Agora);
        }

        conta.LimparEventos();
        return conta;
    }
}
