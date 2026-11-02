namespace Lancamentos.Api.Http;

/// <summary>Correlação da requisição HTTP em curso (garantida por UsarCorrelacao); fora de HTTP, gera uma nova.</summary>
internal sealed class ContextoDaRequisicao(IHttpContextAccessor acessor) : IContextoDaRequisicao
{
    private readonly Lazy<string> correlacao = new(() =>
        acessor.HttpContext?.Request.Headers[Correlacao.Cabecalho].ToString() is { Length: > 0 } valor
            ? valor
            : Guid.NewGuid().ToString());

    public string CorrelacaoId => correlacao.Value;
}
