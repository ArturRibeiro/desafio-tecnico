# Tasks

## 1. Dependências

- [x] 1.1 Adicionar ao `Directory.Packages.props` os pacotes OpenTelemetry 1.20.1 (`Extensions.Hosting`, `Instrumentation.AspNetCore`, `Instrumentation.Http`, `Exporter.OpenTelemetryProtocol` e, só para testes, `Exporter.InMemory`). Referenciar em `Plataforma.Hospedagem`. Verificar:
  - `dotnet build -c Release` termina com 0 warnings;
  - `Arquitetura.Testes` continua passando, sem nova dependência de `Plataforma.Hospedagem` em BC ou contratos.

## 2. Logs estruturados e correlação

- [x] 2.1 Criar `AdicionarObservabilidade(nomeDoServico, fontes)` em `Plataforma.Hospedagem`, com a parte de logs do D3 (`JsonConsole`, escopos, `TraceId` e `SpanId`). Verificar com um teste em `Plataforma.Hospedagem.Testes` que o formatador configurado é o JSON e que os escopos estão habilitados.
- [x] 2.2 Fazer o `UsarCorrelacao` abrir o escopo `CorrelacaoId` e marcar a tag `correlacao.id` no `Activity.Current`; verificar com um teste que um log escrito durante a requisição traz o `CorrelacaoId` recebido no cabeçalho
- [x] 2.3 Chamar `AdicionarObservabilidade` no Gateway, na Lancamentos.Api, na Posicao.Api e no Posicao.Worker. Nos consumers do Worker, abrir o escopo e marcar a tag com o `CorrelacaoId` da mensagem. Verificar que os testes existentes dos quatro serviços continuam passando.

## 3. Traces

- [x] 3.1 Completar `AdicionarObservabilidade` com o tracing do D4:
  - instrumentação de ASP.NET Core, sem `/health/*`, e de `HttpClient`;
  - fontes por nome (`MassTransit` nos serviços com bus, `Npgsql` nos que têm banco);
  - `Resource` com o nome do serviço;
  - OTLP só com `OTEL_EXPORTER_OTLP_ENDPOINT` definido.

  Verificar com testes em `Plataforma.Hospedagem.Testes`:
  - sem o endpoint, nenhum exportador OTLP é registrado e o serviço responde normalmente;
  - com o endpoint, ele é registrado.
- [x] 3.2 Criar `tests/Observabilidade.Testes`, com Lancamentos.Api e Posicao.Worker no mesmo processo, PostgreSQL e RabbitMQ reais e exportador em memória. Verificar:
  - o span do POST de um débito e o span do consumo no Worker têm o mesmo `TraceId`;
  - os dois carregam a tag `correlacao.id` com o `X-Correlation-Id` da resposta.
- [x] 3.3 Verificar no mesmo projeto o cenário do broker fora: registrar com o RabbitMQ parado, religar e confirmar que o consumo pertence ao trace original. Se falhar, aplicar a mitigação do risco R1 (`traceparent` explícito no `PublicadorDeEventos`) e registrar no design.

## 4. Dados sensíveis nos logs

- [x] 4.1 Em `Observabilidade.Testes`, capturar os logs das duas aplicações durante abertura, crédito, débito, estorno e consulta da posição, e uma requisição com token inválido. Verificar que nenhum registro contém o token, o titular, os valores nem as descrições usados. Se algum registro vazar, corrigir a origem.

## 5. Rate limiting no gateway

- [x] 5.1 Ligar a autenticação JWT no gateway (D2), sem autorização. Verificar com testes em `Gateway.Testes` que uma requisição com token inválido continua sendo encaminhada ao serviço de destino.
- [x] 5.2 Implementar o limite do D1:
  - partição por titular ou IP;
  - janela fixa configurável;
  - `429` com `Retry-After` e Problem Details `limite-excedido`;
  - só nas rotas do proxy.

  Verificar com testes, usando o `IStartupFilter` de teste para simular origens:
  - dentro e acima do limite;
  - serviço de destino não recebe a requisição recusada;
  - cliente `A` não afeta `B`;
  - nova janela;
  - mesmo titular de origens diferentes;
  - token forjado preso à origem;
  - saúde com o limite esgotado.
- [x] 5.3 Verificar em `Gateway.Testes` que o serviço de destino recebe um `traceparent` com o `TraceId` do span do gateway

## 6. Ambiente local e documentação

- [x] 6.1 Adicionar o serviço `jaeger` ao `docker-compose.yml` (D5), `ambiente/desenvolvimento/observabilidade.env` nos quatro serviços e `autenticacao.env` no gateway. Verificar:
  - `docker compose up -d --build` deixa todos os containers `healthy`;
  - `docker compose logs lancamentos-api` mostra linhas JSON;
  - um débito pelo gateway aparece no Jaeger com spans do gateway, da Lancamentos.Api e do Posicao.Worker.
- [x] 6.2 Atualizar o README com:
  - o limite de requisições (`429`, `Retry-After` e como ajustar);
  - uma seção de observabilidade (logs JSON, `X-Correlation-Id` e como achar o trace no Jaeger);
  - a porta 16686 na tabela de containers;
  - o projeto `Observabilidade.Testes` na tabela de testes.

  Verificar executando os comandos documentados.
- [x] 6.3 Atualizar o ARQUITETURA.md:
  - coluna "No desafio" de "Implementar" para "Implementado" nos itens já entregues, citando o teste que comprova cada um;
  - "Documentar" mantido onde não há código;
  - rate limiting e observabilidade marcados como implementados.

  Verificar que os links funcionam.

## 7. Verificação integrada

- [x] 7.1 Rodar `dotnet build -c Release` e `dotnet test -c Release` duas vezes seguidas na raiz; verificar 0 warnings e todos os testes passando nas duas

## Workflow follow-up

- Sincronizar as specs e arquivar a change depois da revisão.
