namespace Posicao.Unit.Tests.Dominio;

/// <summary>Monta objetos de domínio válidos para os testes.</summary>
internal static class Fabrica
{
    public static readonly DateTimeOffset Dia1 = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);

    public static ContaId NovaConta() => new(Guid.NewGuid());

    public static TitularId Titular(string valor = "titular-1") => TitularId.Criar(valor).Valor;

    public static Titularidade Titularidade(ContaId conta, string titular = "titular-1") =>
        Posicao.Domain.Contas.Titularidade.Registrar(conta, Titular(titular), Dia1.AddHours(8));

    public static Movimento Credito(ContaId conta, long sequencia, decimal valor, DateTimeOffset competencia, Guid? lancamento = null, decimal? saldoInformado = null) =>
        Criar(conta, sequencia, TipoMovimento.Credito, valor, competencia, lancamento, null, saldoInformado);

    public static Movimento Debito(ContaId conta, long sequencia, decimal valor, DateTimeOffset competencia, Guid? lancamento = null, decimal? saldoInformado = null) =>
        Criar(conta, sequencia, TipoMovimento.Debito, valor, competencia, lancamento, null, saldoInformado);

    public static Movimento Estorno(ContaId conta, long sequencia, Movimento original, DateTimeOffset competencia) =>
        Criar(conta, sequencia, TipoMovimento.Estorno, original.Valor.Quantia, competencia, null, original.LancamentoId.Valor);

    private static Movimento Criar(ContaId conta, long sequencia, TipoMovimento tipo, decimal valor, DateTimeOffset competencia, Guid? lancamento, Guid? estornado, decimal? saldoInformado = null) =>
        Movimento.Criar(
            conta,
            sequencia,
            new LancamentoId(lancamento ?? Guid.NewGuid()),
            tipo,
            Valor.Criar(valor).Valor,
            competencia,
            competencia.AddMilliseconds(5),
            estornado is null ? null : new LancamentoId(estornado.Value),
            saldoInformado).Valor;
}
