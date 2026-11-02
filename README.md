# Sistema de Movimentações Financeiras

Solução para o desafio técnico de arquiteto de software. É um sistema interno de banco digital que **registra movimentações financeiras** (créditos e débitos) e **informa a posição consolidada** (saldo) de uma conta em qualquer instante, e que continua confiável sob alta demanda, instabilidade e falhas parciais.

A solução é modelada com DDD e dividida em microserviços, um por Bounded Context:

- **Lançamentos:** registra as movimentações com consistência forte. É a fonte da verdade.
- **Posição Consolidada:** mantém a posição ao longo do tempo a partir dos eventos de Lançamentos, com consistência eventual.

Os dois contextos só se comunicam por eventos no RabbitMQ (MassTransit 8, com Outbox). O único ponto de entrada externo é um API Gateway (YARP).

> **Estado atual:** os dois Bounded Contexts estão implementados. **Lançamentos** registra aberturas de conta, créditos, débitos e estornos, com autenticação JWT, idempotência, concorrência segura e eventos publicados pelo Outbox. **Posição Consolidada** consome esses eventos e responde o saldo de uma conta em qualquer instante e os fechamentos diários. O gateway limita as requisições de cada cliente, e todos os serviços registram logs em JSON e traces OpenTelemetry, visíveis no Jaeger do ambiente local.

## Documentação

| Documento | Conteúdo |
|---|---|
| [DOMINIO.md](DOMINIO.md) | Modelagem de domínio: Ubiquitous Language, Subdomains, Bounded Contexts, Aggregates, eventos, contratos e decisões de modelagem |
| [ARQUITETURA.md](ARQUITETURA.md) | Índice dos diagramas (DDD estratégico e tático, C4), atributos de qualidade e segurança |
| [diagramas/](diagramas/) | Diagramas em Mermaid, exibidos diretamente pelo GitHub |
| [openspec/](openspec/) | Specs (comportamentos exigidos) e changes (proposta, design e tarefas de cada etapa) |

## Pré-requisitos

| Ferramenta | Versão | Usada para |
|---|---|---|
| [Docker](https://docs.docker.com/get-docker/) com Docker Compose | Docker 24+ e Compose v2 | Subir o ambiente e rodar os cenários de negócio (`Scenarios.Integration.Specs.Tests`, com Testcontainers) |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0.x | Compilar e rodar os testes |

Para apenas **subir o ambiente**, basta o Docker: as imagens são compiladas dentro dos containers.

## Subir o ambiente

```bash
docker compose up -d --build
```

O comando sobe os seguintes containers:

| Container | Função | Exposto no host |
|---|---|---|
| `gateway` | API Gateway, o único ponto de entrada | `http://localhost:8080` |
| `lancamentos-api` | BC Lançamentos | não |
| `posicao-api` | BC Posição Consolidada (consultas) | não |
| `posicao-worker` | BC Posição Consolidada (consumo de eventos) | não |
| `postgres-lancamentos`, `postgres-posicao` | Um banco por Bounded Context | não |
| `rabbitmq` | Broker de mensagens | painel em `http://localhost:15672` |
| `redis` | Cache da posição consolidada | não |
| `jaeger` | Visualizador de traces | interface em `http://localhost:16686` |

Se a porta 8080 já estiver em uso na sua máquina, escolha outra:

```bash
GATEWAY_PORTA=8090 docker compose up -d --build
```

### Verificar que está tudo de pé

Todos os containers devem aparecer como `(healthy)`, o que leva cerca de 30 segundos na primeira subida:

```bash
docker compose ps
```

Saúde do gateway, pelo ponto de entrada:

```bash
curl http://localhost:8080/health/ready
```

A saúde dos serviços internos **não é exposta pelo gateway**: `/lancamentos/health/*` responde `404` de propósito. Para consultá-la, entre na rede interna:

```bash
docker compose exec lancamentos-api curl -s http://localhost:8080/health/ready
```

A resposta lista o estado de cada dependência, sem hosts nem credenciais:

```json
{"estado":"Healthy","dependencias":[{"nome":"postgres","estado":"Healthy"},{"nome":"rabbitmq","estado":"Healthy"}]}
```

| Endpoint | Significado |
|---|---|
| `/health/live` | O processo está respondendo. Não consulta dependências. |
| `/health/ready` | `200` quando as dependências obrigatórias estão acessíveis e `503` quando alguma não está. Na Posição Consolidada, o Redis é opcional: fora do ar, aparece como `Degraded` e a resposta continua `200`. |

### Painel do RabbitMQ

Disponível em `http://localhost:15672`. O usuário e a senha de desenvolvimento estão em [ambiente/desenvolvimento/rabbitmq.env](ambiente/desenvolvimento/rabbitmq.env).

### Credenciais

As credenciais do ambiente local ficam em [ambiente/desenvolvimento/](ambiente/desenvolvimento/), com um arquivo por container. Assim, cada serviço recebe só as credenciais do próprio Bounded Context. Os valores são falsos e servem **somente para desenvolvimento local**. Os `appsettings.json` dos serviços não contêm credenciais: elas chegam por variáveis de ambiente (`ConnectionStrings__Postgres`, `ConnectionStrings__RabbitMq`, `ConnectionStrings__Redis`).

## Usando a API

Todas as chamadas passam pelo gateway (`http://localhost:8080`, ou a porta de `GATEWAY_PORTA`) e exigem um token JWT. No ambiente local, os tokens vêm de um emissor de desenvolvimento.

### 1. Emitir um token

```bash
TOKEN=$(docker compose run --rm -q emissor-de-tokens --titular cliente-1)
```

O `--titular` vira a claim `sub`, que identifica o dono das contas. Por padrão, o token tem os escopos `lancamentos:escrever` e `posicao:ler` e vale 60 minutos. Para mudar, use `--escopos "posicao:ler"` ou `--validade-minutos 10`.

> **Somente desenvolvimento.** O emissor assina com a chave HS256 de [ambiente/desenvolvimento/autenticacao.env](ambiente/desenvolvimento/autenticacao.env). Em produção, os tokens vêm do Provedor de Identidade: configure `Autenticacao__Autoridade` (descoberta OIDC, chaves assimétricas) e não defina a chave simétrica.

### 2. Abrir uma conta

```bash
CONTA=$(curl -s -X POST http://localhost:8080/lancamentos/contas -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: abre-1" | sed -E 's/.*"contaId":"([^"]+)".*/\1/')
```

Toda operação exige o cabeçalho `Idempotency-Key`. Repetir a mesma chave devolve o resultado original, sem duplicar nada. A conta nasce `Ativa`, com saldo e limite zero.

### 3. Creditar e debitar

```bash
curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/creditos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: credito-1" -H "Content-Type: application/json" -d '{"valor": 100.00, "descricao": "Pix recebido"}'
```

```bash
DEBITO=$(curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/debitos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: debito-1" -H "Content-Type: application/json" -d '{"valor": 30.00, "descricao": "Conta de luz"}' | sed -E 's/.*"lancamentoId":"([^"]+)".*/\1/')
```

A `dataCompetencia` é opcional (o padrão é o instante do registro) e, se enviada, precisa ter fuso (`2026-10-27T14:32:10Z`) e estar no dia corrente (UTC).

### 4. Estornar

```bash
curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/lancamentos/$DEBITO/estornos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: estorno-1"
```

Lançamentos nunca são alterados nem excluídos: a correção é sempre um estorno, com efeito inverso.

### 5. Consultar a posição

As consultas exigem o escopo `posicao:ler`, que o token do passo 1 já tem.

Posição atual:

```bash
curl -s http://localhost:8080/posicao/contas/$CONTA -H "Authorization: Bearer $TOKEN"
```

```json
{"contaId":"…","instante":"2026-10-29T13:38:04.77+00:00","saldo":70.00,"moeda":"BRL","atualizadoAte":{"sequencia":3,"instante":"2026-10-29T13:38:02.59+00:00"}}
```

Posição em um instante do passado, que precisa ter fuso e não pode estar no futuro:

```bash
curl -s "http://localhost:8080/posicao/contas/$CONTA?instante=2026-10-29T12:00:00Z" -H "Authorization: Bearer $TOKEN"
```

Fechamentos diários de um período, com `de` e `ate` no formato `yyyy-MM-dd` (no máximo 366 dias). A lista traz só os dias com movimento e é paginada com `pagina` e `tamanhoPagina` (padrão 30, máximo 100):

```bash
curl -s "http://localhost:8080/posicao/contas/$CONTA/fechamentos?de=$(date -u +%Y-%m-01)&ate=$(date -u +%Y-%m-%d)" -H "Authorization: Bearer $TOKEN"
```

A Posição Consolidada é atualizada pelos eventos de Lançamentos, com **consistência eventual**: o saldo pode levar alguns instantes para refletir um lançamento. O campo `atualizadoAte` informa a última movimentação já considerada (a sequência dela na conta e o instante em que foi registrada). Pelo mesmo motivo, uma conta recém-aberta responde `404` até o evento de abertura ser processado.

### Erros

As respostas de erro seguem Problem Details (`application/problem+json`), com um campo `codigo` estável. Por exemplo, um débito acima do saldo responde `422` com `"codigo": "saldo-insuficiente"`. As listas completas estão no DOMINIO.md, nas seções [5.4](DOMINIO.md#54-comandos-casos-de-uso) (Lançamentos) e [6.5](DOMINIO.md#65-consultas) (Posição Consolidada). Uma conta de outro titular responde `404`, igual a uma conta inexistente, para não revelar que ela existe.

### Limite de requisições

O gateway limita as requisições de cada cliente: por padrão, 100 a cada 10 segundos. O cliente é o titular de um token válido; sem token válido, é o endereço de origem. Acima do limite, a resposta é `429` com o código `limite-excedido` e o cabeçalho `Retry-After`, com os segundos até a próxima janela. A saúde do gateway não conta para o limite.

Para mudar o limite, defina `LimiteDeRequisicoes__Permissoes` e `LimiteDeRequisicoes__Janela` (por exemplo, `00:01:00`) no container `gateway`. O limite vale por instância do gateway.

### Eventos

Cada abertura de conta publica `ContaAberta v1`, e cada crédito, débito ou estorno publica `MovimentacaoRegistrada v1`, no RabbitMQ. Eles aparecem como exchanges no painel (`http://localhost:15672`). O contrato está no [DOMINIO.md, seção 7](DOMINIO.md#7-contrato-de-integração).

O `posicao-worker` consome os eventos pelas filas `posicao-conta-aberta` e `posicao-movimentacao-registrada`. Ele descarta repetições e aplica as movimentações de cada conta na ordem em que foram registradas, mesmo que cheguem fora de ordem. Um evento que falha é tentado de novo com intervalo crescente e, se continuar falhando, vai para a fila `_error` correspondente, sem bloquear os demais.

### Reconstruir a posição

Os fechamentos diários podem ser recalculados a partir das movimentações que a Posição Consolidada já recebeu, por exemplo depois de corrigir um defeito de cálculo:

```bash
docker compose run --rm posicao-worker reconstruir
```

O comando também limpa o cache do Redis e pode ser repetido sem efeito adicional.

## Observabilidade

Todos os serviços escrevem os logs em JSON, uma linha por registro, com o `CorrelacaoId` e o `TraceId` de cada operação. Os logs não contêm tokens, titulares, valores nem descrições de lançamentos.

```bash
docker compose logs lancamentos-api
```

Cada operação vira um único trace OpenTelemetry, do gateway até o consumo do evento no `posicao-worker`, mesmo quando a entrega fica adiada pelo Outbox. No ambiente local, os traces vão para o Jaeger: abra `http://localhost:16686`, escolha o serviço `gateway` e filtre pela tag `correlacao.id` com o valor do cabeçalho `X-Correlation-Id` devolvido na resposta. Envie o seu próprio identificador para achar o trace mais fácil:

```bash
curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/creditos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: credito-2" -H "X-Correlation-Id: meu-credito-1" -H "Content-Type: application/json" -d '{"valor": 10.00, "descricao": "Teste de trace"}'
```

Os traces só são exportados quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido; no compose, ele vem de [ambiente/desenvolvimento/observabilidade.env](ambiente/desenvolvimento/observabilidade.env). Sem o Jaeger, os serviços funcionam normalmente.

### Migrações do banco

No ambiente local, a Lancamentos.Api e o Posicao.Worker aplicam as migrações sozinhos ao subir (`Banco__AplicarMigracoesNaSubida=true`) e tentam de novo até o banco estar disponível. A Posicao.Api não migra: só informa `503` na prontidão até o esquema estar atualizado. Em produção, a recomendação é aplicá-las como um passo separado do deploy:

```bash
dotnet ef migrations bundle -p src/Lancamentos/Lancamentos.Infrastructure -s src/Lancamentos/Lancamentos.Api -o efbundle-lancamentos
```

```bash
dotnet ef migrations bundle -p src/Posicao/Posicao.Infrastructure -s src/Posicao/Posicao.Worker -o efbundle-posicao
```

## Parar e limpar

```bash
docker compose down
```

Para também apagar os dados dos bancos:

```bash
docker compose down -v
```

## Compilar e testar

```bash
dotnet build -c Release
```

```bash
dotnet test -c Release
```

A compilação trata **todo warning como erro**. O `Scenarios.Integration.Specs.Tests` sobe PostgreSQL, RabbitMQ e Redis reais com Testcontainers uma vez por execução e, antes de cada cenário, cria bancos e virtual host próprios e sobe as APIs e o Worker com `WebApplicationFactory`; por isso o Docker precisa estar em execução; os demais projetos de teste não precisam dele.

### Cenários de negócio

O `Scenarios.Integration.Specs.Tests` executa, com [SpecFlow](https://github.com/SpecFlowOSS/SpecFlow) (Gherkin) sobre xUnit v2, **todos os cenários das specs de negócio** (`contas`, `titularidade`, `lancamentos`, `eventos-de-integracao`, `projecao-da-posicao` e `consulta-de-posicao`), contra a Lancamentos.Api, a Posicao.Api e o Posicao.Worker rodando no processo. Uma spec é de negócio quando tem um `.feature`; as técnicas (gateway, autenticação, saúde, observabilidade, ambiente e build) não têm e ficam fora.

- Os arquivos `.feature` em [tests/Scenarios.Integration.Specs.Tests/Features](tests/Scenarios.Integration.Specs.Tests/Features/) têm os cenários das specs, um por spec com o nome em PascalCase (`consulta-de-posicao` vira `ConsultaDePosicao.feature`): cada requisito é uma `Rule` e cada cenário, um `Scenario` com os mesmos passos. A change do OpenSpec que cria ou altera cenários de uma spec de negócio atualiza o `.feature` dela e os passos em `Steps`.

- O SpecFlow gera um `.feature.cs` ao lado de cada `.feature` no build; esses arquivos ficam fora do Git.
- Um cenário com passo sem implementação falha.

| Projeto de teste | O que verifica |
|---|---|
| `Architecture.Tests` | Domínio isolado, dependências entre camadas, independência entre Bounded Contexts, MassTransit na v8, casos de uso só pelo Mediator (um handler por pedido, em `UseCases/<Feature>/Imp`) e convenções (nomes dos projetos por camada, usings só no `Usings.cs` e um tipo por arquivo) |
| `Plataforma.Mediator.Unit.Tests` | Entrega de cada pedido ao handler do tipo dele e falha clara quando não há handler |
| `Lancamentos.Unit.Tests` | Domínio de Lançamentos (invariantes, Value Objects, estorno, data de competência), casos de uso e contrato dos eventos comparado ao `DOMINIO.md` |
| `Posicao.Unit.Tests` | Domínio de Posição (ordem por sequência, lacunas, fechamento diário, estorno pelo efeito), consultas, reconstrução e tradução dos eventos de Lançamentos |
| `Scenarios.Integration.Specs.Tests` | Os 69 cenários das 6 specs de negócio, ponta a ponta com PostgreSQL, RabbitMQ e Redis reais: abertura de conta, titularidade, créditos, débitos, estornos, idempotência, concorrência, eventos (inclusive com o broker fora), projeção (fora de ordem, repetidos, inválidos, Worker parado, reconstrução) e consultas. |

O mesmo build e os mesmos testes rodam no CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) a cada push e pull request.

## Estrutura

```
src/
  Lancamentos/     Domain, Application, Infrastructure, Api
  Posicao/         Domain, Application, Infrastructure, Api, Worker
  Gateway/         API Gateway (YARP)
  Compartilhado/   Contratos.Integracao (Published Language), Plataforma.Mediator (Mediator dos casos de uso, sem dependências) e Plataforma.Hospedagem (saúde, correlação, autenticação JWT, observabilidade e registro do Mediator; sem domínio)
tests/             Um projeto de teste por área
ferramentas/       Emissor de tokens de desenvolvimento
ambiente/          Credenciais do ambiente local
openspec/          Specs e changes
```

### Projetos

Os projetos de teste estão na tabela de [Compilar e testar](#compilar-e-testar).

| Projeto | Camada | O que contém | Projetos que referencia |
|---|---|---|---|
| [`Lancamentos.Domain`](src/Lancamentos/Lancamentos.Domain/) | Domínio | Aggregate Root `Conta`, Entity `Lancamento`, Value Objects, Domain Events e códigos de falha de negócio | nada |
| [`Lancamentos.Application`](src/Lancamentos/Lancamentos.Application/) | Aplicação | Casos de uso (abertura de conta, crédito, débito e estorno) em `UseCases/<Feature>/Imp`, com Command, Handler e `Responses`, e as portas em `Portas/` (repositórios e unidade de trabalho) | `Lancamentos.Domain`, `Plataforma.Mediator` |
| [`Lancamentos.Infrastructure`](src/Lancamentos/Lancamentos.Infrastructure/) | Infraestrutura | EF Core com PostgreSQL (`DbContext`, repositórios, unidade de trabalho com `SELECT ... FOR UPDATE`, migrações), Outbox do MassTransit e tradução de Domain Events para os contratos de integração | `Lancamentos.Application`, `Lancamentos.Domain`, `Contratos.Integracao` |
| [`Lancamentos.Api`](src/Lancamentos/Lancamentos.Api/) | Borda (serviço) | API HTTP de Lançamentos: endpoints, validação de entrada, Problem Details, autenticação, saúde e configuração do MassTransit | `Lancamentos.Infrastructure`, `Lancamentos.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Posicao.Domain`](src/Posicao/Posicao.Domain/) | Domínio | Aggregate Roots `FechamentoDiario` e `Titularidade`, Entity `Movimento`, Domain Service `ProjecaoDaConta` e Value Objects próprios | nada |
| [`Posicao.Application`](src/Posicao/Posicao.Application/) | Aplicação | Casos de uso (projeção dos eventos, reconstrução e consultas de posição e de fechamentos) em `UseCases/<Feature>/Imp`, e as portas em `Portas/` (repositórios, unidade de trabalho e cache) | `Posicao.Domain`, `Plataforma.Mediator` |
| [`Posicao.Infrastructure`](src/Posicao/Posicao.Infrastructure/) | Infraestrutura | EF Core com PostgreSQL (repositórios, lock por conta, migrações), Inbox do MassTransit, cache Redis e tradução dos eventos de Lançamentos para o modelo do contexto | `Posicao.Application`, `Posicao.Domain`, `Contratos.Integracao` |
| [`Posicao.Api`](src/Posicao/Posicao.Api/) | Borda (serviço) | API HTTP de consulta da posição e dos fechamentos diários, com autenticação e saúde | `Posicao.Infrastructure`, `Posicao.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Posicao.Worker`](src/Posicao/Posicao.Worker/) | Borda (serviço) | Consumidores de `ContaAberta v1` e `MovimentacaoRegistrada v1`, migrações na subida e o comando `reconstruir` | `Posicao.Infrastructure`, `Posicao.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Gateway`](src/Gateway/Gateway/) | Borda (serviço) | API Gateway com YARP: roteamento, bloqueio da saúde interna, limite de requisições por cliente e correlação | `Plataforma.Hospedagem` |
| [`Contratos.Integracao`](src/Compartilhado/Contratos.Integracao/) | Compartilhado | Published Language: os Integration Events `ContaAberta v1` e `MovimentacaoRegistrada v1` | nada |
| [`Plataforma.Mediator`](src/Compartilhado/Plataforma.Mediator/) | Compartilhado | Mediator dos casos de uso: `IRequest`, `IRequestHandler` e `IMediator` na raiz e a implementação em `Imp/` | nada |
| [`Plataforma.Hospedagem`](src/Compartilhado/Plataforma.Hospedagem/) | Compartilhado (só a borda usa) | Saúde dos serviços, correlação, autenticação JWT, logs e traces e o registro dos handlers no Mediator; sem conceitos de domínio | `Plataforma.Mediator` |
| [`EmissorDeTokens`](ferramentas/EmissorDeTokens/) | Ferramenta | Emite tokens JWT para o ambiente local (`docker compose run emissor-de-tokens`) | nada |

As dependências apontam sempre para o Domínio, e nenhum Bounded Context referencia o outro. Essas regras são verificadas por testes, e não apenas descritas. Os motivos estão em [openspec/changes/archive/2026-10-28-estruturar-solucao/design.md](openspec/changes/archive/2026-10-28-estruturar-solucao/design.md).

## Como o trabalho é planejado (OpenSpec)

Cada etapa é uma *change* do [OpenSpec](https://github.com/Fission-AI/OpenSpec), com proposta (por quê), specs (o que o sistema deve fazer, em cenários testáveis), design (como e quais alternativas foram descartadas) e tarefas. Ao concluir uma change, as specs dela são promovidas para `openspec/specs/`, que descreve o comportamento atual do sistema.

| Change | Situação |
|---|---|
| [estruturar-solucao](openspec/changes/archive/2026-10-28-estruturar-solucao/) | Concluída e arquivada |
| [implementar-bc-lancamentos](openspec/changes/archive/2026-10-29-implementar-bc-lancamentos/) | Concluída e arquivada |
| [implementar-bc-posicao-consolidada](openspec/changes/archive/2026-10-29-implementar-bc-posicao-consolidada/) | Concluída e arquivada |
| [proteger-e-observar-o-gateway](openspec/changes/archive/2026-10-29-proteger-e-observar-o-gateway/) | Concluída e arquivada |
