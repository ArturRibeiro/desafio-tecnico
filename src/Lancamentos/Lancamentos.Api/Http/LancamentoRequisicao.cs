namespace Lancamentos.Api.Http;

internal sealed record LancamentoRequisicao(decimal? Valor, string? Descricao, DateTimeOffset? DataCompetencia);
