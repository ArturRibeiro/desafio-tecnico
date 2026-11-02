namespace Lancamentos.Unit.Tests.Dominio;

public sealed class ValueObjectsTestes
{
    [Theory(DisplayName = "Valor dentro das regras é aceito")]
    [InlineData("0.01")]
    [InlineData("10.5")]
    [InlineData("10.500")]
    [InlineData("999999999999.99")]
    public void Valor_DentroDasRegras_EhAceito(string quantia)
    {
        var resultado = global::Lancamentos.Domain.Comum.Valor.Criar(decimal.Parse(quantia, System.Globalization.CultureInfo.InvariantCulture));

        resultado.Sucesso.Should().BeTrue();
    }

    [Theory(DisplayName = "Valor fora das regras é recusado sem arredondar")]
    [InlineData("0")]
    [InlineData("-10.00")]
    [InlineData("10.005")]
    [InlineData("1000000000000.00")]
    public void Valor_ForaDasRegras_EhRecusadoSemArredondar(string quantia)
    {
        var resultado = global::Lancamentos.Domain.Comum.Valor.Criar(decimal.Parse(quantia, System.Globalization.CultureInfo.InvariantCulture));

        resultado.Falha?.Codigo.Should().Be(CodigosDeFalha.ValorInvalido);
    }

    [Fact(DisplayName = "Descrição com espaços nas pontas é aparada")]
    public void Descricao_ComEspacosNasPontas_EhAparada()
    {
        (global::Lancamentos.Domain.Comum.Descricao.Criar("  Pix recebido  ").Valor.Valor).Should().Be("Pix recebido");
    }

    [Theory(DisplayName = "Descrição vazia é recusada")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Descricao_Vazia_EhRecusada(string? texto)
    {
        (global::Lancamentos.Domain.Comum.Descricao.Criar(texto).Falha?.Codigo).Should().Be(CodigosDeFalha.DescricaoInvalida);
    }

    [Fact(DisplayName = "Descrição com 200 caracteres é aceita e com 201 é recusada")]
    public void Descricao_Com200Caracteres_EhAceitaECom201_EhRecusada()
    {
        (global::Lancamentos.Domain.Comum.Descricao.Criar(new string('a', 200)).Sucesso).Should().BeTrue();
        (global::Lancamentos.Domain.Comum.Descricao.Criar(new string('a', 201)).Falha?.Codigo).Should().Be(CodigosDeFalha.DescricaoInvalida);
    }

    [Fact(DisplayName = "Chave de idempotência ausente é recusada por ser obrigatória")]
    public void ChaveIdempotencia_Ausente_EhObrigatoria()
    {
        ChaveIdempotencia.Criar(null).Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaObrigatoria);
        ChaveIdempotencia.Criar("").Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaObrigatoria);
    }

    [Theory(DisplayName = "Chave de idempotência com caracteres não visíveis ou fora do ASCII é inválida")]
    [InlineData("com espaco")]
    [InlineData("acentuação")]
    [InlineData("quebra\nde-linha")]
    public void ChaveIdempotencia_ComCaracteresNaoVisiveisOuNaoAscii_EhInvalida(string chave)
    {
        ChaveIdempotencia.Criar(chave).Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaInvalida);
    }

    [Fact(DisplayName = "Chave de idempotência com 100 caracteres é aceita e com 101 é inválida")]
    public void ChaveIdempotencia_Com100Caracteres_EhAceitaECom101_EhInvalida()
    {
        ChaveIdempotencia.Criar(new string('k', 100)).Sucesso.Should().BeTrue();
        ChaveIdempotencia.Criar(new string('k', 101)).Falha?.Codigo.Should().Be(CodigosDeFalha.ChaveIdempotenciaInvalida);
    }

    [Fact(DisplayName = "Titular é obrigatório e tem tamanho máximo")]
    public void TitularId_ValidaPresencaETamanho()
    {
        TitularId.Criar("auth0|abc").Sucesso.Should().BeTrue();
        TitularId.Criar(" ").Sucesso.Should().BeFalse();
        TitularId.Criar(new string('t', 201)).Sucesso.Should().BeFalse();
    }

    [Fact(DisplayName = "Identificadores gerados não são consecutivos")]
    public void Identificadores_NaoSaoConsecutivos()
    {
        var a = ContaId.Novo().Valor.ToByteArray();
        var b = ContaId.Novo().Valor.ToByteArray();

        b.Should().NotEqual(a);
        (new System.Numerics.BigInteger(b)).Should().NotBe(new System.Numerics.BigInteger(a) + 1);
    }
}
