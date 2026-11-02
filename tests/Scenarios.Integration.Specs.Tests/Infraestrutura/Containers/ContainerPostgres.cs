namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Containers;

/// <summary>PostgreSQL da execução. Cada cenário cria e apaga os seus bancos nele.</summary>
public static class ContainerPostgres
{
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:16-alpine").WithCommand("-c", "max_connections=500").Build();

    public static Task IniciarAsync() => Container.StartAsync();

    public static async Task EncerrarAsync() => await Container.DisposeAsync();

    public static async Task<string> CriarBancoAsync(string nome)
    {
        await ExecutarSqlAsync($"CREATE DATABASE {nome};");
        return new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { Database = nome, MaxPoolSize = 20 }.ConnectionString;
    }

    public static async Task ApagarBancoAsync(string nome) => await ExecutarSqlAsync($"DROP DATABASE IF EXISTS {nome} WITH (FORCE);");

    /// <summary>Recusa novas conexões ao banco e derruba as abertas; <paramref name="disponivel"/> = true desfaz.</summary>
    public static async Task AlterarDisponibilidadeDoBancoAsync(string nome, bool disponivel)
    {
        await ExecutarSqlAsync($"ALTER DATABASE {nome} WITH ALLOW_CONNECTIONS {(disponivel ? "true" : "false")};");
        if (!disponivel)
        {
            await ExecutarSqlAsync($"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{nome}';");
        }
    }

    private static async Task ExecutarSqlAsync(string sql)
    {
        var resultado = await Container.ExecScriptAsync(sql);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException($"SQL falhou ({sql}): {resultado.Stderr}");
        }
    }
}
