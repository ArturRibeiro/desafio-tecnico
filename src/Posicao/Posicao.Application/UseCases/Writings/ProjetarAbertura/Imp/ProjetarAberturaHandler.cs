namespace Posicao.Application.UseCases.Writings.ProjetarAbertura.Imp;

/// <summary>Cria a titularidade a partir de <c>ContaAberta v1</c> e aplica os movimentos que chegaram antes dela.</summary>
public sealed class ProjetarAberturaHandler(IUnitOfWork unidade, ITitularidadeRepository titularidades, AplicacaoDePendentes pendentes)
    : IRequestHandler<ProjetarAberturaCommand, ResumoDaProjecao>
{
    public Task<ResumoDaProjecao> HandleAsync(ProjetarAberturaCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return unidade.ExecutarNaContaAsync(
            comando.ContaId,
            async ct =>
            {
                if (await titularidades.ObterAsync(comando.ContaId, ct).ConfigureAwait(false) is not null)
                {
                    return new ResumoDaProjecao(JaRecebido: true, 0, null);
                }

                var titularidade = Titularidade.Registrar(comando.ContaId, comando.Titular, comando.DataAbertura);
                titularidades.Adicionar(titularidade);
                return await pendentes.AplicarAsync(titularidade, novo: null, ct).ConfigureAwait(false);
            },
            cancelamento);
    }
}
