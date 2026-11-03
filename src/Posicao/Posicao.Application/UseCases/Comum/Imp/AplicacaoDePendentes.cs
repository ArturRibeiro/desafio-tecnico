namespace Posicao.Application.UseCases.Comum.Imp;

/// <summary>
/// Carrega os pendentes e os originais dos estornos, chama o <see cref="ProjecaoDaConta"/> e registra as divergências
/// de saldo que ele apontar.
/// </summary>
public sealed class AplicacaoDePendentes(
    IMovimentoRepository movimentos,
    IFechamentoDiarioRepository fechamentos,
    IRegistroDeDivergencias divergencias)
{
    /// <param name="novo">Movimento recém-adicionado nesta transação, que a consulta ao banco ainda não enxerga.</param>
    public async Task<ResumoDaProjecao> AplicarAsync(Titularidade titularidade, Movimento? novo, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(titularidade);

        var gravados = await movimentos.ObterAPartirDeAsync(titularidade.ContaId, titularidade.UltimaSequenciaAplicada, cancelamento).ConfigureAwait(false);
        var candidatos = novo is null ? gravados : [.. gravados, novo];
        if (!candidatos.Any(m => m.Sequencia == titularidade.UltimaSequenciaAplicada + 1))
        {
            return new ResumoDaProjecao(JaRecebido: false, 0, null);
        }

        var estornados = candidatos
            .Where(m => m.LancamentoEstornadoId.HasValue)
            .Select(m => m.LancamentoEstornadoId!.Value)
            .ToList();
        var originais = estornados.Count == 0
            ? []
            : await movimentos.ObterPorLancamentosAsync(titularidade.ContaId, estornados, cancelamento).ConfigureAwait(false);
        var ultimo = await fechamentos.ObterUltimoAsync(titularidade.ContaId, cancelamento).ConfigureAwait(false);

        var resultado = ProjecaoDaConta.Aplicar(titularidade, ultimo, candidatos, originais);
        foreach (var fechamento in resultado.NovosFechamentos)
        {
            fechamentos.Adicionar(fechamento);
        }

        foreach (var divergencia in resultado.Divergencias)
        {
            divergencias.Registrar(divergencia);
        }

        return new ResumoDaProjecao(JaRecebido: false, resultado.Aplicados.Count, resultado.Interrupcao);
    }
}
