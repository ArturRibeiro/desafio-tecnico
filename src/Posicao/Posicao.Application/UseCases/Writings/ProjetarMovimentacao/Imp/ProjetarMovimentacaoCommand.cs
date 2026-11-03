namespace Posicao.Application.UseCases.Writings.ProjetarMovimentacao.Imp;

/// <summary>Movimento traduzido de <c>MovimentacaoRegistrada v1</c>, ainda não gravado.</summary>
public sealed record ProjetarMovimentacaoCommand(Movimento Movimento) : IRequest<ResumoDaProjecao>;
