namespace Lancamentos.Unit.Tests.UseCases;

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
