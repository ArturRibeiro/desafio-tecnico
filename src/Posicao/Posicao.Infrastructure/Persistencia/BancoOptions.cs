namespace Posicao.Infrastructure.Persistencia;

public sealed class BancoOptions
{
    /// <summary>
    /// Aplica as migrações quando o serviço sobe (ambiente local e testes). Em produção, a recomendação é
    /// aplicá-las como passo separado do deploy, com um bundle do EF. Só o Worker aplica; a Api apenas verifica.
    /// </summary>
    public bool AplicarMigracoesNaSubida { get; set; }

    public TimeSpan IntervaloEntreTentativas { get; set; } = TimeSpan.FromSeconds(5);
}
