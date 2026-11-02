namespace Lancamentos.Domain.Contas;

/// <summary>
/// Aggregate Root do contexto de Lançamentos (DOMINIO.md, seção 5.1). É a única porta de entrada para criar lançamentos.
/// Não guarda a coleção de lançamentos: mantém o saldo e a sequência e produz um <see cref="Lancamento"/> a cada operação,
/// que é persistido na mesma transação.
/// </summary>
public sealed class Conta
{
    private readonly List<IEventoDeDominio> eventos = [];

    private Conta()
    {
        // Usado pelo EF Core.
    }

    public ContaId Id { get; private set; }

    /// <summary>Cliente dono da conta. Definido na abertura e nunca alterado (invariante 5).</summary>
    public TitularId Titular { get; private set; }

    /// <summary>Chave de idempotência da abertura, única por titular.</summary>
    public ChaveIdempotencia ChaveAbertura { get; private set; }

    public decimal SaldoDisponivel { get; private set; }

    /// <summary>Quanto a conta pode ficar negativa. Toda conta nasce com limite zero.</summary>
    public decimal Limite { get; private set; }

    public SituacaoConta Situacao { get; private set; }

    public long UltimaSequencia { get; private set; }

    /// <summary>Controle de concorrência otimista; incrementado a cada alteração.</summary>
    public int Versao { get; private set; }

    public DateTimeOffset DataAbertura { get; private set; }

    /// <summary>Eventos de domínio ainda não publicados.</summary>
    public IReadOnlyList<IEventoDeDominio> Eventos => eventos;

    public static Conta Abrir(ContaId id, TitularId titular, ChaveIdempotencia chaveAbertura, DateTimeOffset agora)
    {
        var conta = new Conta
        {
            Id = id,
            Titular = titular,
            ChaveAbertura = chaveAbertura,
            SaldoDisponivel = 0m,
            Limite = 0m,
            Situacao = SituacaoConta.Ativa,
            UltimaSequencia = 0,
            Versao = 1,
            DataAbertura = Instantes.Normalizar(agora),
        };
        conta.eventos.Add(new ContaAberta(Guid.CreateVersion7(), conta));
        return conta;
    }

    public bool PertenceA(TitularId titular) => Titular == titular;

    public Resultado<Lancamento> RegistrarCredito(Valor valor, Descricao descricao, DateTimeOffset? dataCompetencia, ChaveIdempotencia chave, DateTimeOffset agora)
    {
        var falha = ValidarAtiva() ?? ValidarCompetencia(dataCompetencia, agora);
        if (falha is not null)
        {
            return Resultado.Falhar<Lancamento>(falha);
        }

        var lancamento = Aplicar(TipoLancamento.Credito, +valor.Quantia, valor, descricao, dataCompetencia ?? agora, agora, null, chave);
        eventos.Add(new LancamentoRegistrado(Guid.CreateVersion7(), lancamento));
        return Resultado.Ok(lancamento);
    }

    public Resultado<Lancamento> RegistrarDebito(Valor valor, Descricao descricao, DateTimeOffset? dataCompetencia, ChaveIdempotencia chave, DateTimeOffset agora)
    {
        var falha = ValidarAtiva() ?? ValidarCompetencia(dataCompetencia, agora) ?? ValidarSaldoPara(valor);
        if (falha is not null)
        {
            return Resultado.Falhar<Lancamento>(falha);
        }

        var lancamento = Aplicar(TipoLancamento.Debito, -valor.Quantia, valor, descricao, dataCompetencia ?? agora, agora, null, chave);
        eventos.Add(new LancamentoRegistrado(Guid.CreateVersion7(), lancamento));
        return Resultado.Ok(lancamento);
    }

    /// <summary>
    /// Anula <paramref name="original"/> com um lançamento de efeito inverso, sujeito às mesmas invariantes da conta:
    /// estornar um crédito retira dinheiro e pode esbarrar no saldo insuficiente.
    /// </summary>
    /// <param name="jaEstornado">Se o original já tem estorno; consultado pela Aplicação, porque a Conta não guarda os lançamentos.</param>
    public Resultado<Lancamento> Estornar(Lancamento original, bool jaEstornado, Descricao? descricao, ChaveIdempotencia chave, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (original.ContaId != Id)
        {
            return Resultado.Falhar<Lancamento>(CodigosDeFalha.LancamentoNaoEncontrado, "Lançamento não encontrado nesta conta.");
        }

        if (original.Tipo == TipoLancamento.Estorno)
        {
            return Resultado.Falhar<Lancamento>(CodigosDeFalha.EstornoNaoEstornavel, "Um estorno não pode ser estornado.");
        }

        if (jaEstornado)
        {
            return Resultado.Falhar<Lancamento>(CodigosDeFalha.LancamentoJaEstornado, "O lançamento já foi estornado.");
        }

        var estornaCredito = original.Tipo == TipoLancamento.Credito;
        var falha = ValidarAtiva() ?? (estornaCredito ? ValidarSaldoPara(original.Valor) : null);
        if (falha is not null)
        {
            return Resultado.Falhar<Lancamento>(falha);
        }

        var efeito = estornaCredito ? -original.Valor.Quantia : +original.Valor.Quantia;
        var texto = descricao ?? Descricao.Criar($"Estorno do lançamento {original.Id}").Valor;
        var estorno = Aplicar(TipoLancamento.Estorno, efeito, original.Valor, texto, agora, agora, original.Id, chave);
        eventos.Add(new LancamentoEstornado(Guid.CreateVersion7(), estorno));
        return Resultado.Ok(estorno);
    }

    public void LimparEventos() => eventos.Clear();

    private Lancamento Aplicar(
        TipoLancamento tipo,
        decimal efeito,
        Valor valor,
        Descricao descricao,
        DateTimeOffset dataCompetencia,
        DateTimeOffset agora,
        LancamentoId? estornado,
        ChaveIdempotencia chave)
    {
        // Invariante 4: o saldo só muda por meio de um lançamento. Invariante 3: sequência sem lacunas.
        SaldoDisponivel += efeito;
        UltimaSequencia++;
        Versao++;
        return Lancamento.Criar(Id, tipo, valor, descricao, Instantes.Normalizar(dataCompetencia), Instantes.Normalizar(agora), UltimaSequencia, estornado, chave, SaldoDisponivel);
    }

    // Invariante 1.
    private Falha? ValidarAtiva() =>
        Situacao == SituacaoConta.Ativa ? null : new Falha(CodigosDeFalha.ContaInativa, "A conta não está ativa.");

    // Invariante 2: o saldo nunca fica abaixo de -Limite.
    private Falha? ValidarSaldoPara(Valor retirada) =>
        SaldoDisponivel - retirada.Quantia >= -Limite ? null : new Falha(CodigosDeFalha.SaldoInsuficiente, "Saldo insuficiente.");

    /// <summary>
    /// A competência fica entre o início do dia corrente (UTC) e agora; assim, dias já fechados
    /// na posição consolidada nunca mudam.
    /// </summary>
    private static Falha? ValidarCompetencia(DateTimeOffset? dataCompetencia, DateTimeOffset agora)
    {
        if (dataCompetencia is not { } competencia)
        {
            return null;
        }

        var inicioDoDia = new DateTimeOffset(agora.UtcDateTime.Date, TimeSpan.Zero);
        return competencia >= inicioDoDia && competencia <= agora
            ? null
            : new Falha(CodigosDeFalha.DataCompetenciaInvalida, "A data de competência deve estar entre o início do dia corrente (UTC) e agora.");
    }
}
