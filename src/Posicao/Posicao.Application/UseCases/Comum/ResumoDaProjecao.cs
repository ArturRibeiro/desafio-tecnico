namespace Posicao.Application.UseCases.Comum;

/// <param name="JaRecebido">O evento já tinha sido gravado antes: nada mudou.</param>
/// <param name="Aplicados">Quantos movimentos foram aplicados agora (inclusive pendentes destravados).</param>
/// <param name="Interrupcao">Motivo de a aplicação ter parado antes da lacuna, se houver.</param>
public sealed record ResumoDaProjecao(bool JaRecebido, int Aplicados, Falha? Interrupcao);
