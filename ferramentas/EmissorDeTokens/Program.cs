// Uso: EmissorDeTokens --titular <id> [--escopos "lancamentos:escrever posicao:ler"] [--validade-minutos 60] [--quantidade N]
// Com --quantidade, emite N tokens, um por linha, para os titulares <id>-1 a <id>-N (usado pelo teste de carga).
// Chave, emissor e audiência vêm das variáveis Autenticacao__ChaveDeAssinatura, Autenticacao__Emissor e Autenticacao__Audiencia.
var argumentos = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 0; i + 1 < args.Length; i += 2)
{
    argumentos[args[i]] = args[i + 1];
}

if (!argumentos.TryGetValue("--titular", out var titular) || string.IsNullOrWhiteSpace(titular))
{
    await Console.Error.WriteLineAsync("Uso: EmissorDeTokens --titular <id> [--escopos \"lancamentos:escrever posicao:ler\"] [--validade-minutos 60] [--quantidade N]");
    return 2;
}

var chave = Environment.GetEnvironmentVariable("Autenticacao__ChaveDeAssinatura");
var emissor = Environment.GetEnvironmentVariable("Autenticacao__Emissor");
var audiencia = Environment.GetEnvironmentVariable("Autenticacao__Audiencia");
if (string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(emissor) || string.IsNullOrWhiteSpace(audiencia))
{
    await Console.Error.WriteLineAsync("Defina Autenticacao__ChaveDeAssinatura, Autenticacao__Emissor e Autenticacao__Audiencia (veja ambiente/desenvolvimento/autenticacao.env).");
    return 2;
}

var escopos = argumentos.GetValueOrDefault("--escopos", Emissor.EscoposPadrao);
var minutos = int.Parse(argumentos.GetValueOrDefault("--validade-minutos", "60"), CultureInfo.InvariantCulture);

var validade = TimeSpan.FromMinutes(minutos);

if (!argumentos.TryGetValue("--quantidade", out var quantidadeInformada))
{
    Console.WriteLine(Emissor.Emitir(chave, emissor, audiencia, titular, escopos, validade));
    return 0;
}

if (!int.TryParse(quantidadeInformada, NumberStyles.None, CultureInfo.InvariantCulture, out var quantidade) || quantidade < 1)
{
    await Console.Error.WriteLineAsync("--quantidade precisa ser um inteiro maior que zero.");
    return 2;
}

for (var i = 1; i <= quantidade; i++)
{
    Console.WriteLine(Emissor.Emitir(chave, emissor, audiencia, $"{titular}-{i}", escopos, validade));
}

return 0;
