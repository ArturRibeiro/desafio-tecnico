namespace Posicao.Domain.Fechamentos;

/// <summary>
/// O saldo calculado pela Posição depois de um movimento difere do saldo disponível que Lançamentos informou para a
/// mesma sequência (DOMINIO.md, seção 6.8). Só identifica o ponto da divergência; os valores ficam fora, como nos logs.
/// </summary>
public sealed record Divergencia(ContaId ContaId, long Sequencia, LancamentoId LancamentoId);
