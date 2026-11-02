namespace Plataforma.Hospedagem;

public static class Configuracao
{
    /// <summary>
    /// Lê uma connection string obrigatória. As credenciais vêm de variáveis de ambiente
    /// (ConnectionStrings__Nome), nunca dos appsettings versionados.
    /// A mensagem de erro cita só o nome da chave, nunca o valor.
    /// </summary>
    public static string ConnectionStringObrigatoria(this IConfiguration configuracao, string nome)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        var valor = configuracao.GetConnectionString(nome);
        return string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException($"Connection string '{nome}' não configurada. Defina a variável de ambiente ConnectionStrings__{nome}.")
            : valor;
    }
}
