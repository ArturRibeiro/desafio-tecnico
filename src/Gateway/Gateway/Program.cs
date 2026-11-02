var builder = WebApplication.CreateBuilder(args);
builder.AdicionarObservabilidade("gateway");

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// O token só identifica o cliente para o limite de requisições: sem UseAuthorization, o gateway não recusa nada por
// autenticação. Token inválido ou ausente segue para o serviço, que responde 401 ou 403.
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration.GetSection("Autenticacao"));
builder.Services.AdicionarLimiteDeRequisicoes(builder.Configuration.GetSection("LimiteDeRequisicoes"));

// O gateway não tem dependências obrigatórias: a prontidão só indica que ele responde.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UsarCorrelacao();
app.BloquearSaudeInterna();
app.UseAuthentication();
app.UseRateLimiter();
app.MapearSaude();
app.MapReverseProxy().RequireRateLimiting(LimiteDeRequisicoes.Politica);

await app.RunAsync();
