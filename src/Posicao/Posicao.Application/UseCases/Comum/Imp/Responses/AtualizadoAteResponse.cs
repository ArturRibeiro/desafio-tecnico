namespace Posicao.Application.UseCases.Comum.Imp.Responses;

/// <summary>Até onde os dados da conta estão atualizados: a última sequência aplicada e o instante de registro dela.</summary>
public sealed record AtualizadoAteResponse(long Sequencia, DateTimeOffset Instante)
{
    public static AtualizadoAteResponse De(Titularidade titularidade)
    {
        ArgumentNullException.ThrowIfNull(titularidade);
        return new(titularidade.UltimaSequenciaAplicada, titularidade.AtualizadoAte);
    }
}
