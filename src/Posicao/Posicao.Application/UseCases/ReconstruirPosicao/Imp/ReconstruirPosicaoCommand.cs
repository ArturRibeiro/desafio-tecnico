namespace Posicao.Application.UseCases.ReconstruirPosicao.Imp;

/// <summary>Reconstrução de todas as contas; a resposta é quantas foram reconstruídas.</summary>
public sealed record ReconstruirPosicaoCommand : IRequest<int>;
