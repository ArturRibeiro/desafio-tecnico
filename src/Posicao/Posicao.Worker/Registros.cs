namespace Posicao.Worker;

/// <summary>Logs do consumo: só identificadores, nunca titular nem valores.</summary>
internal static partial class Registros
{
    public static void Projetado(ILogger log, string evento, Guid eventoId, string correlacaoId, ResumoDaProjecao resumo)
    {
        if (resumo.Interrupcao is { } interrupcao)
        {
            AplicacaoInterrompida(log, evento, eventoId, correlacaoId, interrupcao.Codigo, interrupcao.Mensagem);
        }
        else
        {
            EventoProjetado(log, evento, eventoId, correlacaoId, resumo.JaRecebido, resumo.Aplicados);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Evento} {EventoId} projetado (correlação {CorrelacaoId}): já recebido = {JaRecebido}, aplicados = {Aplicados}.")]
    private static partial void EventoProjetado(ILogger log, string evento, Guid eventoId, string correlacaoId, bool jaRecebido, int aplicados);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Evento} {EventoId} (correlação {CorrelacaoId}): aplicação da conta interrompida ({Codigo}). {Mensagem} O movimento segue pendente.")]
    private static partial void AplicacaoInterrompida(ILogger log, string evento, Guid eventoId, string correlacaoId, string codigo, string mensagem);
}
