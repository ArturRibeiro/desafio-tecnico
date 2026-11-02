namespace Lancamentos.Infrastructure.Eventos;

/// <summary>
/// Domain Event → Integration Event. É a única ponte entre o modelo interno e a Published Language:
/// o Domínio pode mudar sem quebrar o contrato, e uma versão nova do contrato só toca este arquivo.
/// </summary>
public static class TradutorDeEventos
{
    public static ContaAbertaV1 Traduzir(ContaAberta evento, string correlacaoId)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return new ContaAbertaV1
        {
            EventoId = evento.EventoId,
            ContaId = evento.Conta.Id.Valor,
            TitularId = evento.Conta.Titular.Valor,
            Moeda = Valor.Moeda,
            DataAbertura = evento.Conta.DataAbertura,
            CorrelacaoId = correlacaoId,
        };
    }

    public static MovimentacaoRegistradaV1 Traduzir(Guid eventoId, Lancamento lancamento, string correlacaoId)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        return new MovimentacaoRegistradaV1
        {
            EventoId = eventoId,
            ContaId = lancamento.ContaId.Valor,
            LancamentoId = lancamento.Id.Valor,
            Sequencia = lancamento.Sequencia,
            Tipo = lancamento.Tipo.ToString(),
            Valor = lancamento.Valor.Quantia,
            Moeda = Valor.Moeda,
            DataCompetencia = lancamento.DataCompetencia,
            DataRegistro = lancamento.DataRegistro,
            LancamentoEstornadoId = lancamento.LancamentoEstornadoId?.Valor,
            CorrelacaoId = correlacaoId,
        };
    }
}
