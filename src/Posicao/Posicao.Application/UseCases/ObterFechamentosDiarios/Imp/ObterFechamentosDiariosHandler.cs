namespace Posicao.Application.UseCases.ObterFechamentosDiarios.Imp;

/// <summary>Fechamentos dos dias com movimento no período, em ordem crescente de data e paginados.</summary>
public sealed class ObterFechamentosDiariosHandler(IRepositorioDeTitularidades titularidades, IRepositorioDeFechamentos fechamentos)
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

        var titularidade = await titularidades.ObterAsync(consulta.ContaId, cancelamento).ConfigureAwait(false);
        if (titularidade is null || !titularidade.PertenceA(consulta.Titular))
        {
            return Resultado.Falhar<FechamentosDiariosResponse>(Falhas.ContaNaoEncontrada);
        }

        // Um item a mais só para saber se há próxima página.
        var lidos = await fechamentos.ListarAsync(
            consulta.ContaId,
            new DataFechamento(de),
            new DataFechamento(ate),
            (consulta.Pagina - 1) * consulta.TamanhoPagina,
            consulta.TamanhoPagina + 1,
            cancelamento).ConfigureAwait(false);

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
