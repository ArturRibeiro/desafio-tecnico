namespace Posicao.Application.UseCases.Queries.ObterFechamentosDiarios.Imp;

/// <summary>Fechamentos dos dias com movimento no período, em ordem crescente de data e paginados.</summary>
public sealed class ObterFechamentosDiariosHandler(IDbContextReadOnly contextReadOnly)
    : IRequestHandler<ObterFechamentosDiariosQuery, Resultado<FechamentosDiariosResponse>>
{
    public const int TamanhoMaximoDaPagina = 100;
    public const int DiasMaximosDoPeriodo = 366;

    public async Task<Resultado<FechamentosDiariosResponse>> HandleAsync(ObterFechamentosDiariosQuery consulta, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (consulta.De is not { } de || consulta.Ate is not { } ate
            || de > ate
            || ate.DayNumber - de.DayNumber > DiasMaximosDoPeriodo
            || consulta.Pagina < 1
            || consulta.TamanhoPagina is < 1 or > TamanhoMaximoDaPagina)
        {
            return Resultado.Falhar<FechamentosDiariosResponse>(
                CodigosDeFalha.PeriodoInvalido,
                "Informe de e ate (yyyy-MM-dd), com de <= ate e no máximo 366 dias, e uma página de 1 a 100 itens.");
        }

        var conta = consulta.ContaId;
        var titularidade = await contextReadOnly.Query<Titularidade>()
            .AsNoTracking()
            .Where(t => t.ContaId == conta)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);
        if (titularidade is null || !titularidade.PertenceA(consulta.Titular))
        {
            return Resultado.Falhar<FechamentosDiariosResponse>(Falhas.ContaNaoEncontrada);
        }

        // Um item a mais só para saber se há próxima página.
        var inicio = new DataFechamento(de);
        var fim = new DataFechamento(ate);
        var lidos = await contextReadOnly.Query<FechamentoDiario>()
            .AsNoTracking()
            .Where(f => f.ContaId == conta && f.Data >= inicio && f.Data <= fim)
            .OrderBy(f => f.Data)
            .Skip((consulta.Pagina - 1) * consulta.TamanhoPagina)
            .Take(consulta.TamanhoPagina + 1)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return Resultado.Ok(new FechamentosDiariosResponse(
            consulta.ContaId.Valor,
            Valor.Moeda,
            lidos.Take(consulta.TamanhoPagina).Select(FechamentoDiarioResponse.De).ToList(),
            consulta.Pagina,
            consulta.TamanhoPagina,
            lidos.Count > consulta.TamanhoPagina,
            AtualizadoAteResponse.De(titularidade)));
    }
}
