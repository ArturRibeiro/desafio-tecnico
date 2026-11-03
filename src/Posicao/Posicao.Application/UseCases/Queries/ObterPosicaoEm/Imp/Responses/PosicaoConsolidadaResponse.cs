namespace Posicao.Application.UseCases.Queries.ObterPosicaoEm.Imp.Responses;

/// <summary>Posição consolidada: saldo da conta num instante e até onde o dado está atualizado (DOMINIO.md, seção 6.3).</summary>
public sealed record PosicaoConsolidadaResponse(Guid ContaId, DateTimeOffset Instante, decimal Saldo, string Moeda, AtualizadoAteResponse AtualizadoAte);
