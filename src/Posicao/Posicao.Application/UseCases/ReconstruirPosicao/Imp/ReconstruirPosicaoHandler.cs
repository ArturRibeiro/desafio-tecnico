namespace Posicao.Application.UseCases.ReconstruirPosicao.Imp;

/// <summary>
/// Recalcula os fechamentos de todas as contas a partir dos movimentos já recebidos (DOMINIO.md, seção 6.6) e
/// limpa o cache, que pode guardar saldos calculados de fechamentos errados. Concilia de novo cada movimento com o saldo
/// informado por Lançamentos e registra as divergências.
/// </summary>
public sealed class ReconstruirPosicaoHandler(
    IUnidadeDeTrabalho unidade,
    IRepositorioDeTitularidades titularidades,
    IRepositorioDeMovimentos movimentos,
    IRepositorioDeFechamentos fechamentos,
    ICacheDeSaldos cache,
    IRegistroDeDivergencias divergencias)
    : IRequestHandler<ReconstruirPosicaoCommand, int>
{
    /// <returns>Quantas contas foram reconstruídas.</returns>
    public async Task<int> HandleAsync(ReconstruirPosicaoCommand comando, CancellationToken cancelamento)
    {
        var contas = await titularidades.ListarContasAsync(cancelamento).ConfigureAwait(false);
        foreach (var conta in contas)
        {
            await unidade.ExecutarNaContaAsync(conta, ct => ReconstruirAsync(conta, ct), cancelamento).ConfigureAwait(false);
        }

        await cache.LimparAsync(cancelamento).ConfigureAwait(false);
        return contas.Count;
    }

    private async Task<bool> ReconstruirAsync(ContaId conta, CancellationToken cancelamento)
    {
        var titularidade = await titularidades.ObterAsync(conta, cancelamento).ConfigureAwait(false);
        if (titularidade is null)
        {
            return false;
        }

        await fechamentos.RemoverTodosAsync(conta, cancelamento).ConfigureAwait(false);
        var todos = await movimentos.ObterAPartirDeAsync(conta, 0, cancelamento).ConfigureAwait(false);
        var resultado = ProjecaoDaConta.Reconstruir(titularidade, todos);
        foreach (var fechamento in resultado.NovosFechamentos)
        {
            fechamentos.Adicionar(fechamento);
        }

        foreach (var divergencia in resultado.Divergencias)
        {
            divergencias.Registrar(divergencia);
        }

        return true;
    }
}
