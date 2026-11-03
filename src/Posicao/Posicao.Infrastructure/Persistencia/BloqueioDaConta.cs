namespace Posicao.Infrastructure.Persistencia;

/// <summary>Linha devolvida pela função <c>bloquear_conta</c>: só a conta bloqueada. Não é um conceito do domínio.</summary>
internal sealed class BloqueioDaConta
{
    public Guid ContaId { get; private set; }
}
