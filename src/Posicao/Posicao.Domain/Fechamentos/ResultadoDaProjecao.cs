namespace Posicao.Domain.Fechamentos;

/// <param name="Aplicados">Movimentos aplicados nesta chamada, em ordem de sequência.</param>
/// <param name="NovosFechamentos">Fechamentos criados nesta chamada (dias que ainda não tinham fechamento).</param>
/// <param name="Interrupcao">Motivo de a aplicação ter parado antes de uma lacuna; nulo se parou só por falta do próximo movimento.</param>
public sealed record ResultadoDaProjecao(IReadOnlyList<Movimento> Aplicados, IReadOnlyList<FechamentoDiario> NovosFechamentos, Falha? Interrupcao);
