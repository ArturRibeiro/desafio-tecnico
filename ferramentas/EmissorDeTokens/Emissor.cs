namespace EmissorDeTokens;

/// <summary>
/// Emite tokens JWT HS256 aceitos pelos serviços no ambiente local. SOMENTE PARA DESENVOLVIMENTO:
/// em produção os tokens vêm do Provedor de Identidade, com chaves assimétricas.
/// </summary>
public static class Emissor
{
    public const string EscoposPadrao = "lancamentos:escrever posicao:ler";

    public static string Emitir(string chaveBase64, string emissor, string audiencia, string titular, string escopos, TimeSpan validade, DateTimeOffset? agora = null)
    {
        var instante = (agora ?? DateTimeOffset.UtcNow).UtcDateTime;
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = audiencia,
            Subject = new ClaimsIdentity([new Claim("sub", titular), new Claim("scope", escopos)]),
            IssuedAt = instante,
            NotBefore = instante,
            Expires = instante.Add(validade),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(chaveBase64)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descritor);
    }
}
