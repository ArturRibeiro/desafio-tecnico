namespace Posicao.Application.UseCases.ObterPosicaoEm.Imp;

/// <summary>
/// Posição = saldo de abertura do dia do instante + efeitos dos movimentos aplicados desse dia até o instante.
/// No máximo três leituras indexadas, qualquer que seja o tamanho do histórico.
/// </summary>
public sealed class ObterPosicaoEmHandler(
    IRepositorioDeTitularidades titularidades,
    IRepositorioDeMovimentos movimentos,
    IRepositorioDeFechamentos fechamentos,
    ICacheDeSaldos cache,
    TimeProvider relogio)
    : IRequestHandler<ObterPosicaoEmQuery, Resultado<PosicaoConsolidadaResponse>>
{
    public async Task<Resultado<PosicaoConsolidadaResponse>> HandleAsync(ObterPosicaoEmQuery consulta, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var agora = relogio.GetUtcNow();
        if (consulta.Instante > agora)
        {
            return Resultado.Falhar<PosicaoConsolidadaResponse>(CodigosDeFalha.InstanteInvalido, "O instante não pode estar no futuro.");
        }

        var instante = (consulta.Instante ?? agora).ToUniversalTime();
        var titularidade = await titularidades.ObterAsync(consulta.ContaId, cancelamento).ConfigureAwait(false);
        if (titularidade is null || !titularidade.PertenceA(consulta.Titular))
        {
            return Resultado.Falhar<PosicaoConsolidadaResponse>(Falhas.ContaNaoEncontrada);
        }

        var dia = DataFechamento.De(instante);
        var abertura = await SaldoDeAberturaAsync(consulta.ContaId, dia, cancelamento).ConfigureAwait(false);
        var doDia = await movimentos.SomarAplicadosAsync(consulta.ContaId, dia.Inicio, instante, titularidade.UltimaSequenciaAplicada, cancelamento).ConfigureAwait(false);

        return Resultado.Ok(new PosicaoConsolidadaResponse(consulta.ContaId.Valor, instante, Dinheiro.DuasCasas(abertura + doDia), Valor.Moeda, AtualizadoAteResponse.De(titularidade)));
    }

    /// <summary>
    /// Fechamento do último dia anterior com movimento. Vai para o cache só quando é definitivo: a conta já tem
    /// fechamento no dia ou depois, e nenhum movimento futuro cai num dia anterior (DOMINIO.md, seção 6.1, invariante 4).
    /// </summary>
    private async Task<decimal> SaldoDeAberturaAsync(ContaId conta, DataFechamento dia, CancellationToken cancelamento)
    {
        if (await Cache.TentarAsync(() => cache.ObterAberturaAsync(conta, dia, cancelamento)).ConfigureAwait(false) is { } emCache)
        {
            return emCache;
        }

        var anterior = await fechamentos.ObterUltimoAntesDeAsync(conta, dia, cancelamento).ConfigureAwait(false);
        var abertura = anterior?.SaldoFechamento ?? 0m;
        var ultimo = await fechamentos.ObterUltimoAsync(conta, cancelamento).ConfigureAwait(false);
        if (ultimo is not null && ultimo.Data >= dia)
        {
            await Cache.TentarAsync(async () =>
            {
                await cache.GuardarAberturaAsync(conta, dia, abertura, cancelamento).ConfigureAwait(false);
                return (decimal?)null;
            }).ConfigureAwait(false);
        }

        return abertura;
    }
}
