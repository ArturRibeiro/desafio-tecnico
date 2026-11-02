namespace Plataforma.Hospedagem;

/// <summary>Opções da seção "Autenticacao" (variáveis Autenticacao__*).</summary>
public sealed class OpcoesDeAutenticacao
{
    /// <summary>Emissor esperado no "iss". Obrigatório.</summary>
    public string? Emissor { get; set; }

    /// <summary>Audiência esperada no "aud". Obrigatória.</summary>
    public string? Audiencia { get; set; }

    /// <summary>
    /// URL do Provedor de Identidade (descoberta OIDC e JWKS, chaves assimétricas). É o caminho de produção.
    /// </summary>
    public Uri? Autoridade { get; set; }

    /// <summary>
    /// Chave simétrica HS256 em Base64, com pelo menos 256 bits. Só para o ambiente local e os testes;
    /// ignorada quando <see cref="Autoridade"/> está preenchida.
    /// </summary>
    public string? ChaveDeAssinatura { get; set; }
}
