namespace Posicao.Worker;

/// <summary>Opções da seção "Consumo" (variáveis Consumo__*).</summary>
public sealed class OpcoesDeConsumo
{
    /// <summary>Prefixo das filas: {prefixo}-conta-aberta e {prefixo}-movimentacao-registrada (e as _error de cada uma).</summary>
    public string PrefixoDasFilas { get; set; } = "posicao";

    /// <summary>Mensagens processadas ao mesmo tempo por fila. A ordem por conta vem do lock da conta, não deste limite.</summary>
    public int LimiteDeMensagensConcorrentes { get; set; } = 16;

    public int Retentativas { get; set; } = 5;

    public TimeSpan IntervaloMinimo { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan IntervaloMaximo { get; set; } = TimeSpan.FromSeconds(30);
}
