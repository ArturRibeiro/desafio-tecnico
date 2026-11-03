namespace Posicao.Domain.Comum;

/// <summary>
/// Marca um Aggregate Root (DOMINIO.md, seção 4.3): a fronteira de consistência que é carregada e gravada inteira,
/// pelo seu próprio Repository. Só Aggregate Root tem Repository, e os testes de arquitetura verificam isso.
/// </summary>
public interface IAggregateRoot
{
}
