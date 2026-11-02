namespace Contratos.Integracao.Lancamentos;

/// <summary>
/// Published Language de Lançamentos: uma conta foi aberta (DOMINIO.md, seção 7).
/// É sempre o primeiro evento da conta (sequência 0) e o único que leva o <see cref="TitularId"/>.
/// Mudanças incompatíveis exigem um novo tipo (V2), que convive com este.
/// </summary>
public sealed record ContaAbertaV1
{
    public string TipoEvento { get; init; } = "ContaAberta";

    public int Versao { get; init; } = 1;

    public required Guid EventoId { get; init; }

    public required Guid ContaId { get; init; }

    /// <summary>Identificador pseudônimo do titular (claim "sub" do Provedor de Identidade). Nunca dados cadastrais.</summary>
    public required string TitularId { get; init; }

    public long Sequencia { get; init; }

    public required string Moeda { get; init; }

    public required DateTimeOffset DataAbertura { get; init; }

    public required string CorrelacaoId { get; init; }
}
