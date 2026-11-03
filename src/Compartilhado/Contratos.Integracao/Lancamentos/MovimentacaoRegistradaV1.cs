namespace Contratos.Integracao.Lancamentos;

/// <summary>
/// Published Language de Lançamentos: um crédito, débito ou estorno foi registrado (DOMINIO.md, seção 7).
/// Não carrega o titular nem a descrição do lançamento. A entrega é "pelo menos uma vez" e sem ordem garantida:
/// o consumidor descarta repetições pelo <see cref="EventoId"/> e ordena pela <see cref="Sequencia"/>.
/// </summary>
public sealed record MovimentacaoRegistradaV1
{
    public string TipoEvento { get; init; } = "MovimentacaoRegistrada";

    public int Versao { get; init; } = 1;

    public required Guid EventoId { get; init; }

    public required Guid ContaId { get; init; }

    public required Guid LancamentoId { get; init; }

    /// <summary>Ordem do lançamento na conta, começando em 1 e sem lacunas.</summary>
    public required long Sequencia { get; init; }

    /// <summary>"Credito", "Debito" ou "Estorno".</summary>
    public required string Tipo { get; init; }

    /// <summary>Sempre positivo; o sinal vem do <see cref="Tipo"/>. Trafega como texto decimal ("150.00") para não perder precisão.</summary>
    public required decimal Valor { get; init; }

    public required string Moeda { get; init; }

    public required DateTimeOffset DataCompetencia { get; init; }

    public required DateTimeOffset DataRegistro { get; init; }

    /// <summary>Lançamento anulado, quando <see cref="Tipo"/> é "Estorno".</summary>
    public Guid? LancamentoEstornadoId { get; init; }

    /// <summary>
    /// Saldo disponível da conta logo após a movimentação, para o consumidor conciliar o saldo que calculou.
    /// Opcional para manter a v1 compatível: avisos publicados antes do campo não o trazem.
    /// </summary>
    public decimal? SaldoDisponivelApos { get; init; }

    public required string CorrelacaoId { get; init; }
}
