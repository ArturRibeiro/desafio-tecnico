namespace Gateway;

/// <summary>
/// Os endpoints de saúde dos serviços internos são consultados pela rede interna, nunca pelo ponto de entrada público.
/// O bloqueio é explícito, para continuar valendo mesmo que a regra de prefixo das rotas mude.
/// O roteamento do YARP não diferencia maiúsculas, então o bloqueio também não.
/// </summary>
internal static partial class SaudeInterna
{
    public static IApplicationBuilder BloquearSaudeInterna(this IApplicationBuilder app) =>
        app.Use(async (contexto, proximo) =>
        {
            if (CaminhoDeSaudeInterna().IsMatch(contexto.Request.Path.Value ?? string.Empty))
            {
                contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await proximo(contexto).ConfigureAwait(false);
        });

    [GeneratedRegex("^/(lancamentos|posicao)/health(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CaminhoDeSaudeInterna();
}
