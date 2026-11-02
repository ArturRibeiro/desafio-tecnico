namespace Posicao.Infrastructure.Eventos;

/// <summary>
/// Camada anticorrupção: Published Language de Lançamentos → modelo de Posição. É o único ponto que conhece os
/// contratos; uma versão nova do contrato só toca este arquivo.
/// </summary>
public static class TradutorDeEventos
{
    public static ProjetarAberturaCommand Traduzir(ContaAbertaV1 evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (evento.Moeda != Valor.Moeda || evento.Sequencia != 0 || evento.ContaId == Guid.Empty)
        {
            throw new EventoInvalidoException($"ContaAberta {evento.EventoId} fora do contrato (moeda, sequência ou conta).");
        }

        var titular = TitularId.Criar(evento.TitularId);
        return titular.Falha is null
            ? new ProjetarAberturaCommand(new ContaId(evento.ContaId), titular.Valor, evento.DataAbertura)
            : throw new EventoInvalidoException($"ContaAberta {evento.EventoId} sem titular válido.");
    }

    public static Movimento Traduzir(MovimentacaoRegistradaV1 evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // Só os nomes exatos do contrato; Enum.TryParse aceitaria também "1" ou "debito".
        var tipo = evento.Tipo switch
        {
            "Credito" => TipoMovimento.Credito,
            "Debito" => TipoMovimento.Debito,
            "Estorno" => TipoMovimento.Estorno,
            _ => throw new EventoInvalidoException($"MovimentacaoRegistrada {evento.EventoId} com tipo desconhecido."),
        };

        if (evento.Moeda != Valor.Moeda || evento.ContaId == Guid.Empty)
        {
            throw new EventoInvalidoException($"MovimentacaoRegistrada {evento.EventoId} fora do contrato (moeda ou conta).");
        }

        var valor = Valor.Criar(evento.Valor);
        if (valor.Falha is { } falhaDoValor)
        {
            throw new EventoInvalidoException($"MovimentacaoRegistrada {evento.EventoId}: {falhaDoValor.Mensagem}");
        }

        var movimento = Movimento.Criar(
            new ContaId(evento.ContaId),
            evento.Sequencia,
            new LancamentoId(evento.LancamentoId),
            tipo,
            valor.Valor,
            evento.DataCompetencia,
            evento.DataRegistro,
            evento.LancamentoEstornadoId is { } estornado ? new LancamentoId(estornado) : null);
        return movimento.Falha is { } falha
            ? throw new EventoInvalidoException($"MovimentacaoRegistrada {evento.EventoId}: {falha.Mensagem}")
            : movimento.Valor;
    }
}
