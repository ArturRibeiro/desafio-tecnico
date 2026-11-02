namespace Lancamentos.Unit.Tests.Dominio;

public sealed class EscalaDoValorTestes
{
    [Theory(DisplayName = "Valor do lançamento tem sempre duas casas decimais")]
    [InlineData("30", "30.00")]
    [InlineData("10.5", "10.50")]
    [InlineData("10.500", "10.50")]
    public void Valor_TemSempreDuasCasas(string entrada, string esperado)
    {
        var valor = global::Lancamentos.Domain.Comum.Valor.Criar(decimal.Parse(entrada, System.Globalization.CultureInfo.InvariantCulture)).Valor;

        valor.Quantia.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be(esperado);
    }
}
