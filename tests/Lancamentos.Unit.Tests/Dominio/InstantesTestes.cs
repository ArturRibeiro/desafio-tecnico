namespace Lancamentos.Unit.Tests.Dominio;

public sealed class InstantesTestes
{
    [Fact(DisplayName = "Normalização do instante converte para UTC e trunca em microssegundos")]
    public void Normalizar_ConverteParaUtcETruncaEmMicrossegundos()
    {
        var instante = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1234567);

        var normalizado = Instantes.Normalizar(instante);

        normalizado.Offset.Should().Be(TimeSpan.Zero);
        normalizado.AddTicks(7).Should().Be(instante);
        (normalizado.Ticks % 10).Should().Be(0);
    }
}
