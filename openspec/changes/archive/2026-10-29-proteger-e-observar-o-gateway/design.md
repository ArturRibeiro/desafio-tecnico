# Design

## Context

A motivação está em [proposal.md](proposal.md). O comportamento exigido está nas specs `api-gateway`, `observabilidade` e `ambiente-local`.

Estado atual:

- **Gateway** (YARP): roteia, bloqueia a saúde interna e garante o `X-Correlation-Id` (`Plataforma.Hospedagem.Correlacao`). Não conhece JWT.
- **Correlação nos dados:** Lancamentos.Api e Posicao.Api já devolvem o `correlacaoId` nos Problem Details. Lançamentos o grava em cada evento, e o Posicao.Worker o registra nos logs do consumo.
- **Logs:** console padrão do .NET, em texto. Não há OpenTelemetry em nenhum serviço.
- **Hospedagem compartilhada:** `Plataforma.Hospedagem` é usada pelo Gateway, pelas duas Apis e pelo Worker. Ela não pode depender de Bounded Contexts nem dos contratos (spec `qualidade-de-build`).

## Goals / Non-Goals

**Goals:**

- Uma única configuração de logs e traces, em `Plataforma.Hospedagem`, chamada pelos quatro serviços.
- Limite por cliente que um cliente abusivo não contorna trocando o token.
- Verificação automatizada do trace de Lançamentos até a projeção no Worker e da ausência de dados sensíveis nos logs.

**Non-Goals:**

- Métricas e alertas. Só traces e logs.
- Limite compartilhado entre instâncias do gateway, que exigiria um store distribuído, como o Redis.

## Decisions

### D1. Rate limiting no gateway

O gateway usa o middleware nativo do ASP.NET Core (`AddRateLimiter`), com `PartitionedRateLimiter` e `FixedWindowRateLimiter` por partição, sem fila (`QueueLimit = 0`).

**Partição:**

- Usa `titular:{sub}` quando `HttpContext.User` está autenticado.
- Caso contrário, usa `ip:{Connection.RemoteIpAddress}`.
- O `X-Forwarded-For` não é usado, porque o cliente o forja. Atrás de um balanceador, a origem real exige `ForwardedHeaders` com `KnownProxies`, o que fica documentado como evolução.

**Opções** (seção `LimiteDeRequisicoes`):

| Opção | Padrão |
|---|---|
| `Permissoes` | 100 |
| `Janela` | `00:00:10` |

**Recusa:** `OnRejected` responde `429` com:

- o cabeçalho `Retry-After`, em segundos, arredondado para cima a partir de `MetadataName.RetryAfter`;
- `application/problem+json` com `codigo = limite-excedido` e o `correlacaoId`.

**Escopo:** o limite vale só para as rotas do proxy (`MapReverseProxy().RequireRateLimiting(...)`). Assim a saúde do gateway fica fora dele, sem uma regra especial.

**Ordem dos middlewares:**

1. `UsarCorrelacao`
2. `BloquearSaudeInterna`
3. `UseAuthentication`
4. `UseRateLimiter`
5. endpoints

*Alternativas descartadas:*

- **Token bucket:** a janela fixa tem um `Retry-After` exato e mais simples de testar. Para o desafio, a diferença em rajadas na virada da janela não importa.
- **Limite nos serviços:** o gateway é o ponto de entrada único, e o excesso nem chega aos serviços.

### D2. Identificação pelo token no gateway

O gateway chama `AdicionarAutenticacaoJwt` (a mesma de `Plataforma.Hospedagem`) e `UseAuthentication`, mas **não** usa `UseAuthorization` nem exige autenticação nas rotas:

- Token válido: o `User` fica autenticado com o `sub`.
- Token inválido ou ausente: o `User` fica anônimo, e a requisição segue para o serviço, que responde `401` ou `403`.

O `OnTokenValidated` que já existe recusa um token sem `sub`, então ele cai na partição por IP. O gateway passa a receber `autenticacao.env` no compose.

*Alternativa descartada:* ler o `sub` sem validar a assinatura. Um cliente trocaria o `sub` a cada requisição e nunca seria limitado.

### D3. Logs estruturados

`Plataforma.Hospedagem` ganha `builder.AdicionarObservabilidade("<servico>", fontes...)`:

- **Formato:** só o provedor `JsonConsole`, com `IncludeScopes = true` e timestamp UTC no formato ISO 8601.
- **Trace nos logs:** `LoggerFactoryOptions.ActivityTrackingOptions = TraceId | SpanId`, que põe os identificadores do trace no escopo de cada registro.
- **Correlação nas requisições:** o `UsarCorrelacao` passa a abrir um escopo de log `CorrelacaoId` e a marcar `Activity.Current` com a tag `correlacao.id`. Isso vale para as requisições dos quatro serviços.
- **Correlação no consumo:** os consumers do Worker abrem o mesmo escopo e marcam a mesma tag com o `CorrelacaoId` da mensagem. Fica no consumer, e não num filtro do MassTransit, porque o campo vem do contrato e a biblioteca de hospedagem não conhece os contratos.

**Dados sensíveis:** o que já existe garante a maior parte.

- O IdentityModel esconde os dados pessoais e o token por padrão (`ShowPII = false`).
- O EF não usa `EnableSensitiveDataLogging`.
- O `HttpLogging` não é habilitado.
- Os logs próprios só têm identificadores.

O teste do fluxo completo (D6) é o que garante que isso continua valendo.

### D4. Traces OpenTelemetry

A mesma extensão registra `AddOpenTelemetry().WithTracing(...)`:

- **Instrumentação:**
  - `AddAspNetCoreInstrumentation`, sem os caminhos `/health/*`, que geram ruído;
  - `AddHttpClientInstrumentation`, para o YARP propagar o `traceparent`.
- **Fontes por nome**, passadas pelo serviço: `"MassTransit"` e `"Npgsql"`. Isso evita que `Plataforma.Hospedagem` dependa do MassTransit ou do Npgsql.
- **Nome do serviço:** vai no `Resource`.
- **Exportador OTLP:** registrado só se `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido.

**Pacotes** (todos 1.20.1):

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Exporter.InMemory`, só nos testes

**Continuidade pelo Outbox:** o MassTransit grava os cabeçalhos de diagnóstico (`traceparent`) da publicação em `OutboxMessage`. O consumer abre o span como filho desse contexto, mesmo quando a entrega acontece minutos depois. Isso é verificado pelo teste com o broker fora (risco R1).

*Alternativa descartada:* Serilog. O `JsonConsole` nativo cobre os logs estruturados sem dependência nova, e o OpenTelemetry fica responsável pelos traces.

### D5. Jaeger no ambiente local

O compose ganha o serviço `jaeger` (`jaegertracing/jaeger`, v2, com a versão fixada na implementação):

- recebe OTLP gRPC na porta 4317, só na rede interna;
- publica a interface web na porta 16686 do host, documentada no README como apoio, a mesma exceção já aberta para o painel do RabbitMQ;
- os serviços recebem `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317` por `ambiente/desenvolvimento/observabilidade.env`.

Os serviços não dependem do Jaeger para ficar prontos. Se ele cair, só os traces se perdem.

### D6. Testes

| Projeto | Cobertura |
|---|---|
| `Gateway.Testes` (ampliado) | Limite, `429` com `Retry-After` e Problem Details, isolamento entre clientes, nova janela, saúde fora do limite, partição pelo titular de token válido, token forjado preso ao IP, token inválido encaminhado e `traceparent` propagado ao serviço de destino. |
| `Plataforma.Hospedagem.Testes` (ampliado) | O formatador configurado é o JSON, o escopo `CorrelacaoId` está presente nos registros de uma requisição e o OTLP só é registrado com destino configurado. |
| `Observabilidade.Testes` (novo) | Lancamentos.Api e Posicao.Worker no mesmo processo, com PostgreSQL e RabbitMQ reais (Testcontainers), exportador em memória e um provedor de logs que captura os registros. |

Para simular origens diferentes no `Gateway.Testes`, um `IStartupFilter` registrado **só nos testes** define `Connection.RemoteIpAddress` a partir de um cabeçalho de teste. O `TestServer` não tem IP de origem.

O `Observabilidade.Testes` verifica:

- o mesmo `TraceId` no POST e no consumo, inclusive com o broker parado durante o registro;
- a tag `correlacao.id` nos spans;
- nenhum log com o token, o titular, os valores ou as descrições.

## Risks / Trade-offs

- **R1. O `traceparent` não sobreviver ao Outbox.** O consumo viraria um trace separado. Mitigação: o teste com o broker fora detecta isso. Se acontecer, o `PublicadorDeEventos` grava o `traceparent` do `Activity.Current` explicitamente nos cabeçalhos da mensagem.
- **R2. O limite é por instância.** Com N instâncias do gateway, um cliente pode fazer até N vezes o limite. Isso é aceitável para o desafio e fica documentado. A solução é um rate limiter distribuído no Redis.
- **R3. No compose, o host chega aos containers por um único IP.** Todos os clientes anônimos da máquina dividem uma partição. Como as chamadas do README usam token, cada uma é limitada pelo titular.
- **R4. Validar o token no gateway duplica configuração.** É a mesma `autenticacao.env` usada pelos serviços, e uma troca de chave atinge todos ao mesmo tempo.
- **R5. Logs em JSON são menos legíveis no terminal.** Em troca, ficam prontos para um agregador. O `docker compose logs` continua utilizável.

## Descobertas da implementação

- **O contexto do trace sobrevive ao Outbox sem ajuste.** O teste com o broker parado durante o registro mostrou o consumo no mesmo trace da requisição. A mitigação do R1 não foi necessária.
- **Jaeger 2.21.0.** A interface fica em `/`, e a API de consulta do Jaeger v2 em `/api/v3`, não mais em `/api`. A imagem não tem shell nem ferramentas, então o container não tem healthcheck. Os serviços também não dependem dele.
- **Logs do healthcheck.** A verificação de migrações roda a cada 5 s no compose e gerava um log Information de SQL do EF a cada vez. A categoria `Microsoft.EntityFrameworkCore.Database.Command` passou para Warning nos três serviços com banco.
- **Verificação de dados sensíveis em todos os níveis.** No `Observabilidade.Testes`, os logs são capturados em Trace, inclusive `Microsoft.AspNetCore`, que o `appsettings` filtra. Assim a verificação cobre o que poderia ser registrado, e não só o que passa pelo filtro. O teste também exige registros da autenticação JWT e do Worker, para não passar vazio.
- **Coleção dos spans nos testes.** Três aplicações exportam spans ao mesmo tempo para a mesma coleção em memória, então ela é protegida por lock.

## Migration Plan

Não há dados nem contratos afetados. Para desligar o limite sem reverter o código, basta subir `LimiteDeRequisicoes__Permissoes`. Para desligar a exportação de traces, basta remover `OTEL_EXPORTER_OTLP_ENDPOINT`.
