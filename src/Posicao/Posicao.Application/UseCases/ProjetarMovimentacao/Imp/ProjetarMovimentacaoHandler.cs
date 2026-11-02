namespace Posicao.Application.UseCases.ProjetarMovimentacao.Imp;

/// <summary>
/// Grava o movimento de <c>MovimentacaoRegistrada v1</c> e aplica, em ordem, tudo o que estiver pronto. Um movimento
/// fora de ordem, ou de uma conta ainda sem titularidade, fica gravado como pendente.
/// </summary>
public sealed class ProjetarMovimentacaoHandler(
    IUnidadeDeTrabalho unidade,
    IRepositorioDeTitularidades titularidades,
    IRepositorioDeMovimentos movimentos,
    AplicacaoDePendentes pendentes)
    : IRequestHandler<ProjetarMovimentacaoCommand, ResumoDaProjecao>
{
    public Task<ResumoDaProjecao> HandleAsync(ProjetarMovimentacaoCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var movimento = comando.Movimento;

        return unidade.ExecutarNaContaAsync(
            movimento.ContaId,
            async ct =>
            {
                // Segunda barreira de idempotência, depois do Inbox: o mesmo lançamento entregue com outro MessageId.
                if (await movimentos.ExisteAsync(movimento.ContaId, movimento.Sequencia, ct).ConfigureAwait(false))
                {
                    return new ResumoDaProjecao(JaRecebido: true, 0, null);
                }

                movimentos.Adicionar(movimento);
                var titularidade = await titularidades.ObterAsync(movimento.ContaId, ct).ConfigureAwait(false);
                return titularidade is null
                    ? new ResumoDaProjecao(JaRecebido: false, 0, null)
                    : await pendentes.AplicarAsync(titularidade, movimento, ct).ConfigureAwait(false);
            },
            cancelamento);
    }
}
