namespace Lancamentos.Infrastructure.Persistencia;

/// <summary>Usado só pelo dotnet-ef para gerar migrações; não abre conexão.</summary>
public sealed class LancamentosDbContextFactory : IDesignTimeDbContextFactory<LancamentosDbContext>
{
    public LancamentosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LancamentosDbContext>().UseNpgsql("Host=design-time").Options);
}
