namespace Plataforma.Hospedagem;

/// <summary>
/// Autenticação JWT comum aos serviços (spec autenticacao). A identidade do titular vem só do token:
/// a claim "sub" é o TitularId, e as permissões vêm da claim "scope", com escopos separados por espaço.
/// </summary>
public static class Autenticacao
{
    public const string ClaimTitular = "sub";
    public const string ClaimEscopos = "scope";
    public const int TamanhoMaximoDoTitular = 200;

    public static IServiceCollection AdicionarAutenticacaoJwt(this IServiceCollection servicos, IConfiguration secao)
    {
        ArgumentNullException.ThrowIfNull(secao);

        var opcoes = secao.Get<AutenticacaoOptions>() ?? new AutenticacaoOptions();
        if (string.IsNullOrWhiteSpace(opcoes.Emissor) || string.IsNullOrWhiteSpace(opcoes.Audiencia))
        {
            throw new InvalidOperationException("Autenticacao:Emissor e Autenticacao:Audiencia são obrigatórios.");
        }

        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Mantém "sub" e "scope" com os nomes originais, sem o mapeamento para os tipos de claim do .NET.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opcoes.Emissor,
                    ValidAudience = opcoes.Audiencia,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTitular,
                };

                if (opcoes.Autoridade is not null)
                {
                    o.Authority = opcoes.Autoridade.ToString();
                }
                else
                {
                    o.TokenValidationParameters.IssuerSigningKey = ChaveSimetrica(opcoes.ChaveDeAssinatura);
                    // Só HS256: impede a troca de algoritmo pelo emissor do token.
                    o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
                }

                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = contexto =>
                    {
                        if (ObterTitular(contexto.Principal) is null)
                        {
                            contexto.Fail("Token sem titular válido (sub).");
                        }

                        return Task.CompletedTask;
                    },
                };
            });
        servicos.AddAuthorization();
        return servicos;
    }

    /// <summary>Exige um token válido que carregue o escopo informado; sem o escopo, a resposta é 403.</summary>
    public static TBuilder ExigirEscopo<TBuilder>(this TBuilder endpoints, string escopo)
        where TBuilder : IEndpointConventionBuilder =>
        endpoints.RequireAuthorization(politica => politica
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto => TemEscopo(contexto.User, escopo)));

    /// <summary>O "sub" do token, se presente, não vazio e com até 200 caracteres.</summary>
    public static string? ObterTitular(ClaimsPrincipal? usuario) =>
        usuario?.FindFirst(ClaimTitular)?.Value is { Length: > 0 and <= TamanhoMaximoDoTitular } titular && !string.IsNullOrWhiteSpace(titular)
            ? titular
            : null;

    public static bool TemEscopo(ClaimsPrincipal usuario, string escopo)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return usuario.FindAll(ClaimEscopos)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(escopo, StringComparer.Ordinal);
    }

    private static SymmetricSecurityKey ChaveSimetrica(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException("Configure Autenticacao:Autoridade (produção) ou Autenticacao:ChaveDeAssinatura (ambiente local).");
        }

        var bytes = Convert.FromBase64String(base64);
        return bytes.Length >= 32
            ? new SymmetricSecurityKey(bytes)
            : throw new InvalidOperationException("Autenticacao:ChaveDeAssinatura precisa ter pelo menos 256 bits.");
    }
}
