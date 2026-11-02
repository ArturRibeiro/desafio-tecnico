namespace Posicao.Application.UseCases.ObterFechamentosDiarios.Imp.Responses;

/// <summary>Página de fechamentos diários de um período.</summary>
public sealed record FechamentosDiariosResponse(
    Guid ContaId,
    string Moeda,
    IReadOnlyList<FechamentoDiarioResponse> Itens,
    int Pagina,
    int TamanhoPagina,
    bool TemProximaPagina,
    AtualizadoAteResponse AtualizadoAte);
