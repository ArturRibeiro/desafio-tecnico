namespace Architecture.Tests;

/// <summary>
/// Spec "Versão principal do MassTransit limitada": a v9 tem licença comercial. O FluentAssertions segue a mesma
/// regra: a v8 tem licença comercial.
/// </summary>
public sealed class DependenciasTestes
{
    [Fact(DisplayName = "MassTransit está travado na versão principal 8 (licença Apache 2.0)")]
    public void MassTransit_EstaNaVersaoPrincipal8()
    {
        var versao = Solucao.Assembly("MassTransit").GetName().Version;

        versao.Should().NotBeNull();
        versao.Major.Should().Be(8);
    }

    [Fact(DisplayName = "FluentAssertions está travado na versão principal 7 (licença Apache 2.0)")]
    public void FluentAssertions_EstaNaVersaoPrincipal7() =>
        typeof(AssertionExtensions).Assembly.GetName().Version!.Major.Should().Be(7);
}
