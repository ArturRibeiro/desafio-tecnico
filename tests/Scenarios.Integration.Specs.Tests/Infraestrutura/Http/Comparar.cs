namespace Scenarios.Integration.Specs.Tests.Infraestrutura.Http;

/// <summary>Comparações de corpo de resposta que ignoram o que muda a cada requisição.</summary>
public static class Comparar
{
    private static readonly string[] PorRequisicao = ["traceId", "correlacaoId", "instance"];

    /// <summary>Corpo JSON sem traceId, correlacaoId e instance, para comparar respostas de requisições diferentes.</summary>
    public static string SemIdentificadoresDaRequisicao(string corpo)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(corpo)!.AsObject();
        foreach (var campo in PorRequisicao)
        {
            json.Remove(campo);
        }

        return json.ToJsonString();
    }
}
