namespace Posicao.Infrastructure.Persistencia;

/// <summary>Usado só pelo dotnet-ef para gerar migrações; não abre conexão.</summary>
public sealed class FabricaDeDesign : IDesignTimeDbContextFactory<PosicaoDbContext>
{
    public PosicaoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PosicaoDbContext>().UseNpgsql("Host=design-time").Options);
}
