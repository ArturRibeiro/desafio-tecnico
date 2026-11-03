namespace Posicao.Application.UseCases.Queries.ObterPosicaoEm.Imp;

/// <summary>
/// Posição = saldo de abertura do dia do instante + efeitos dos movimentos aplicados desse dia até o instante.
/// No máximo três leituras indexadas, qualquer que seja o tamanho do histórico.
/// </summary>
public sealed class ObterPosicaoEmHandler(
    IDbContextReadOnly contextReadOnly,
    ISaldoCache cache,
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
        var conta = consulta.ContaId;
        var titularidade = await contextReadOnly.Query<Titularidade>()
            .AsNoTracking()
            .Where(t => t.ContaId == conta)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);
        if (titularidade is null || !titularidade.PertenceA(consulta.Titular))
        {
            return Resultado.Falhar<PosicaoConsolidadaResponse>(Falhas.ContaNaoEncontrada);
        }

        var dia = DataFechamento.De(instante);
        var abertura = await SaldoDeAberturaAsync(conta, dia, cancelamento).ConfigureAwait(false);
        var doDia = await SomarAplicadosAsync(conta, dia.Inicio, instante, titularidade.UltimaSequenciaAplicada, cancelamento).ConfigureAwait(false);

        return Resultado.Ok(new PosicaoConsolidadaResponse(conta.Valor, instante, Dinheiro.DuasCasas(abertura + doDia), Valor.Moeda, AtualizadoAteResponse.De(titularidade)));
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

        var anterior = await contextReadOnly.Query<FechamentoDiario>()
            .AsNoTracking()
            .Where(f => f.ContaId == conta && f.Data < dia)
            .OrderByDescending(f => f.Data)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);
        var abertura = anterior?.SaldoFechamento ?? 0m;
        var ultimo = await contextReadOnly.Query<FechamentoDiario>()
            .AsNoTracking()
            .Where(f => f.ContaId == conta)
            .OrderByDescending(f => f.Data)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);
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

    /// <summary>
    /// Soma, no banco, o efeito dos movimentos aplicados no intervalo. O cast por <c>object</c> não converte nada em
    /// memória: diz ao EF para usar a coluna numérica por trás do Value Object <see cref="Valor"/>.
    /// </summary>
    private Task<decimal> SomarAplicadosAsync(ContaId conta, DateTimeOffset de, DateTimeOffset ate, long ateSequencia, CancellationToken cancelamento)
    {
        var inicio = de.ToUniversalTime();
        var fim = ate.ToUniversalTime();
        return contextReadOnly.Query<Movimento>()
            .AsNoTracking()
            .Where(m => m.ContaId == conta
                && m.Efeito != null
                && m.Sequencia <= ateSequencia
                && m.DataCompetencia >= inicio
                && m.DataCompetencia <= fim)
            .SumAsync(m => m.Efeito == Efeito.Credito ? (decimal)(object)m.Valor : -(decimal)(object)m.Valor, cancelamento);
    }
}
