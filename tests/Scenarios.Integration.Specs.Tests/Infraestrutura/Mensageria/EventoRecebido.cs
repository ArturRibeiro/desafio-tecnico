namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Mensageria;

/// <param name="Tipo">Nome do contrato, por exemplo MovimentacaoRegistradaV1.</param>
/// <param name="Mensagem">Corpo do contrato (o campo "message" do envelope do MassTransit).</param>
public sealed record EventoRecebido(string Tipo, JsonElement Mensagem)
{
    public string Texto(string propriedade) => Mensagem.GetProperty(propriedade).ToString();
}
