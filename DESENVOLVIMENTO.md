# Guia de desenvolvimento

Detalhes para quem vai evoluir a solução. Para subir e usar a aplicação localmente, veja o [README.md](README.md).

## Documentação

| Documento | Conteúdo |
|---|---|
| [DOMINIO.md](DOMINIO.md) | Modelagem de domínio: Ubiquitous Language, Subdomains, Bounded Contexts, Aggregates, eventos, contratos e decisões de modelagem |
| [ARQUITETURA.md](ARQUITETURA.md) | Índice dos diagramas (DDD estratégico e tático, C4), atributos de qualidade e segurança |
| [diagramas/](diagramas/) | Diagramas em Mermaid, exibidos diretamente pelo GitHub |
| [openspec/](openspec/) | Specs (comportamentos exigidos) e changes (proposta, design e tarefas de cada etapa) |

## Saúde dos serviços

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

## Credenciais do ambiente local

As credenciais ficam em [ambiente/desenvolvimento/](ambiente/desenvolvimento/), com um arquivo por container. Assim, cada serviço recebe só as credenciais do próprio Bounded Context. Os valores são falsos e servem **somente para desenvolvimento local**. Os `appsettings.json` dos serviços não contêm credenciais: elas chegam por variáveis de ambiente (`ConnectionStrings__Postgres`, `ConnectionStrings__RabbitMq`, `ConnectionStrings__Redis`).

O emissor de tokens assina com a chave HS256 de [ambiente/desenvolvimento/autenticacao.env](ambiente/desenvolvimento/autenticacao.env). Em produção, os tokens vêm do Provedor de Identidade: configure `Autenticacao__Autoridade` (descoberta OIDC, chaves assimétricas) e não defina a chave simétrica.

## Detalhes da API

### Tokens

O `--titular` do emissor vira a claim `sub`, que identifica o dono das contas. Por padrão, o token tem os escopos `lancamentos:escrever` e `posicao:ler` e vale 60 minutos. Para mudar, use `--escopos "posicao:ler"` ou `--validade-minutos 10`.

### Painéis do ambiente local

| Endereço | O que é |
|---|---|
| `http://localhost:15672` | Painel do RabbitMQ (usuário e senha em [ambiente/desenvolvimento/rabbitmq.env](ambiente/desenvolvimento/rabbitmq.env)) |
| `http://localhost:16686` | Jaeger, com os traces de cada operação |

### Idempotência e data de competência

Toda operação exige o cabeçalho `Idempotency-Key`. Repetir a mesma chave devolve o resultado original, sem duplicar nada. A `dataCompetencia` é opcional (o padrão é o instante do registro) e, se enviada, precisa ter fuso (`2026-10-27T14:32:10Z`) e estar no dia corrente (UTC). Lançamentos nunca são alterados nem excluídos: a correção é sempre um estorno, com efeito inverso.

### Consultas

- Posição em um instante: `GET /posicao/contas/{conta}?instante=2026-10-29T12:00:00Z`; o instante precisa ter fuso e não pode estar no futuro.
- Fechamentos diários: `GET /posicao/contas/{conta}/fechamentos?de=yyyy-MM-dd&ate=yyyy-MM-dd` (no máximo 366 dias). A lista traz só os dias com movimento e é paginada com `pagina` e `tamanhoPagina` (padrão 30, máximo 100).
- A Posição Consolidada é atualizada pelos eventos de Lançamentos, com **consistência eventual**: o saldo pode levar alguns instantes para refletir um lançamento. O campo `atualizadoAte` informa a última movimentação já considerada. Pelo mesmo motivo, uma conta recém-aberta responde `404` até o evento de abertura ser processado.

### Erros

As respostas de erro seguem Problem Details (`application/problem+json`), com um campo `codigo` estável. Por exemplo, um débito acima do saldo responde `422` com `"codigo": "saldo-insuficiente"`. As listas completas estão no DOMINIO.md, nas seções [5.4](DOMINIO.md#54-comandos-casos-de-uso) (Lançamentos) e [6.5](DOMINIO.md#65-consultas) (Posição Consolidada). Uma conta de outro titular responde `404`, igual a uma conta inexistente, para não revelar que ela existe.

### Limite de requisições

O gateway limita as requisições de cada cliente: por padrão, 100 a cada 10 segundos. O cliente é o titular de um token válido; sem token válido, é o endereço de origem. Acima do limite, a resposta é `429` com o código `limite-excedido` e o cabeçalho `Retry-After`. A saúde do gateway não conta para o limite.

Para mudar o limite, defina `LimiteDeRequisicoes__Permissoes` e `LimiteDeRequisicoes__Janela` (por exemplo, `00:01:00`) no container `gateway`. O limite vale por instância do gateway.

## Eventos

Cada abertura de conta publica `ContaAberta v1`, e cada crédito, débito ou estorno publica `MovimentacaoRegistrada v1`, no RabbitMQ. Eles aparecem como exchanges no painel (`http://localhost:15672`, usuário e senha em [ambiente/desenvolvimento/rabbitmq.env](ambiente/desenvolvimento/rabbitmq.env)). O contrato está no [DOMINIO.md, seção 7](DOMINIO.md#7-contrato-de-integração).

Cada `MovimentacaoRegistrada v1` traz também o saldo disponível da conta após a movimentação (`saldoDisponivelApos`). O `posicao-worker` compara esse saldo com o que calculou e, se forem diferentes, registra um log de erro com o código `saldo-divergente`, a conta, a sequência e o lançamento, sem interromper a projeção ([DOMINIO.md, seção 6.8](DOMINIO.md#68-conciliação-com-lançamentos)). Para procurar divergências:

```bash
docker compose logs posicao-worker | grep saldo-divergente
```

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

Cada operação vira um único trace OpenTelemetry, do gateway até o consumo do evento no `posicao-worker`, mesmo quando a entrega fica adiada pelo Outbox. No ambiente local, os traces vão para o Jaeger: abra `http://localhost:16686`, escolha o serviço `gateway` e filtre pela tag `correlacao.id` com o valor do cabeçalho `X-Correlation-Id` devolvido na resposta (ou envie o seu próprio identificador nesse cabeçalho).

Os traces só são exportados quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido; no compose, ele vem de [ambiente/desenvolvimento/observabilidade.env](ambiente/desenvolvimento/observabilidade.env). Sem o Jaeger, os serviços funcionam normalmente.

## Migrações do banco

No ambiente local, a Lancamentos.Api e o Posicao.Worker aplicam as migrações sozinhos ao subir (`Banco__AplicarMigracoesNaSubida=true`) e tentam de novo até o banco estar disponível. A Posicao.Api não migra: só informa `503` na prontidão até o esquema estar atualizado. Em produção, a recomendação é aplicá-las como um passo separado do deploy:

```bash
dotnet ef migrations bundle -p src/Lancamentos/Lancamentos.Infrastructure -s src/Lancamentos/Lancamentos.Api -o efbundle-lancamentos
```

```bash
dotnet ef migrations bundle -p src/Posicao/Posicao.Infrastructure -s src/Posicao/Posicao.Worker -o efbundle-posicao
```

## Testes

A compilação trata **todo warning como erro**. O mesmo build e os mesmos testes rodam no CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) a cada push e pull request.

| Projeto de teste | O que verifica |
|---|---|
| `Architecture.Tests` | Domínio isolado, dependências entre camadas, independência entre Bounded Contexts, MassTransit na v8 e FluentAssertions na v7, casos de uso só pelo Mediator (um handler por pedido, em `UseCases/Queries/<Feature>/Imp` ou `UseCases/Writings/<Feature>/Imp`) e convenções (nomes dos projetos por camada, usings só no `Usings.cs`, um tipo por arquivo mapeamentos do EF Core em `Persistencia/EntityConfigurations`, acesso a dados em LINQ, pelos Repositories nas escritas e pelo `IDbContextReadOnly` nas Queries, e Aggregate Roots com um único Repository) |
| `Plataforma.Mediator.Unit.Tests` | Entrega de cada pedido ao handler do tipo dele e falha clara quando não há handler |
| `Lancamentos.Unit.Tests` | Domínio de Lançamentos (invariantes, Value Objects, estorno, data de competência), casos de uso e contrato dos eventos comparado ao `DOMINIO.md` |
| `Posicao.Unit.Tests` | Domínio de Posição (ordem por sequência, lacunas, fechamento diário, estorno pelo efeito), consultas, reconstrução e tradução dos eventos de Lançamentos |
| `Scenarios.Integration.Specs.Tests` | Os 73 cenários das 6 specs de negócio, ponta a ponta com PostgreSQL, RabbitMQ e Redis reais: abertura de conta, titularidade, créditos, débitos, estornos, idempotência, concorrência, eventos (inclusive com o broker fora), projeção (fora de ordem, repetidos, inválidos, Worker parado, reconstrução, conciliação do saldo) e consultas |

### Cenários de negócio

O `Scenarios.Integration.Specs.Tests` executa, com [SpecFlow](https://github.com/SpecFlowOSS/SpecFlow) (Gherkin) sobre xUnit v2, **todos os cenários das specs de negócio** (`contas`, `titularidade`, `lancamentos`, `eventos-de-integracao`, `projecao-da-posicao` e `consulta-de-posicao`). Ele sobe PostgreSQL, RabbitMQ e Redis com Testcontainers uma vez por execução e, antes de cada cenário, cria bancos e virtual host próprios e sobe as APIs e o Worker com `WebApplicationFactory`. Uma spec é de negócio quando tem um `.feature`; as técnicas (gateway, autenticação, saúde, observabilidade, ambiente e build) não têm e ficam fora.

- Os arquivos `.feature` em [tests/Scenarios.Integration.Specs.Tests/Features](tests/Scenarios.Integration.Specs.Tests/Features/) têm os cenários das specs, um por spec com o nome em PascalCase (`consulta-de-posicao` vira `ConsultaDePosicao.feature`): cada requisito é uma `Rule` e cada cenário, um `Scenario` com os mesmos passos. A change do OpenSpec que cria ou altera cenários de uma spec de negócio atualiza o `.feature` dela e os passos em `Steps`.
- O SpecFlow gera um `.feature.cs` ao lado de cada `.feature` no build; esses arquivos ficam fora do Git.
- Um cenário com passo sem implementação falha.

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

| Projeto | Camada | O que contém | Projetos que referencia |
|---|---|---|---|
| [`Lancamentos.Domain`](src/Lancamentos/Lancamentos.Domain/) | Domínio | Aggregate Roots `Conta` e `Lancamento` (`IAggregateRoot`), Value Objects, Domain Events, as interfaces dos repositórios (`IContaRepository`, `ILancamentoRepository`) e códigos de falha de negócio | nada |
| [`Lancamentos.Application`](src/Lancamentos/Lancamentos.Application/) | Aplicação | Casos de uso (abertura de conta, crédito, débito e estorno) em `UseCases/Writings/<Feature>/Imp`, com Command, Handler e `Responses`, e as portas em `Ports/` (unidade de trabalho e contexto da requisição) | `Lancamentos.Domain`, `Plataforma.Mediator` |
| [`Lancamentos.Infrastructure`](src/Lancamentos/Lancamentos.Infrastructure/) | Infraestrutura | EF Core com PostgreSQL (`DbContext`, mapeamentos em `EntityConfigurations`, repositórios em `Repositories`, com a conta carregada por `SELECT ... FOR UPDATE` pelo `BloqueioParaAlteracaoInterceptor`, unidade de trabalho e migrações), Outbox do MassTransit e tradução de Domain Events para os contratos de integração | `Lancamentos.Application`, `Lancamentos.Domain`, `Contratos.Integracao` |
| [`Lancamentos.Api`](src/Lancamentos/Lancamentos.Api/) | Borda (serviço) | API HTTP de Lançamentos: endpoints, validação de entrada, Problem Details, autenticação, saúde e configuração do MassTransit | `Lancamentos.Infrastructure`, `Lancamentos.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Posicao.Domain`](src/Posicao/Posicao.Domain/) | Domínio | Aggregate Roots `FechamentoDiario`, `Titularidade` e `Movimento` (`IAggregateRoot`), Domain Service `ProjecaoDaConta`, Value Objects próprios e as interfaces dos repositórios (`ITitularidadeRepository`, `IMovimentoRepository`, `IFechamentoDiarioRepository`) | nada |
| [`Posicao.Application`](src/Posicao/Posicao.Application/) | Aplicação | Casos de uso em `UseCases/Writings/<Feature>/Imp` (projeção dos eventos e reconstrução) e `UseCases/Queries/<Feature>/Imp` (consultas de posição e de fechamentos), e as portas em `Ports/` (unidade de trabalho, leitura das Queries pelo `IDbContextReadOnly`, cache e registro de divergências) | `Posicao.Domain`, `Plataforma.Mediator` |
| [`Posicao.Infrastructure`](src/Posicao/Posicao.Infrastructure/) | Infraestrutura | EF Core com PostgreSQL (mapeamentos em `EntityConfigurations`, repositórios em `Repositories`, leitura das Queries em `ReadOnly` (`DbContextReadOnly`), lock por conta pela função `bloquear_conta`, consultada pelo `TitularidadeRepository`, e migrações), Inbox do MassTransit, cache Redis e tradução dos eventos de Lançamentos para o modelo do contexto | `Posicao.Application`, `Posicao.Domain`, `Contratos.Integracao` |
| [`Posicao.Api`](src/Posicao/Posicao.Api/) | Borda (serviço) | API HTTP de consulta da posição e dos fechamentos diários, com autenticação e saúde | `Posicao.Infrastructure`, `Posicao.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Posicao.Worker`](src/Posicao/Posicao.Worker/) | Borda (serviço) | Consumidores de `ContaAberta v1` e `MovimentacaoRegistrada v1`, migrações na subida e o comando `reconstruir` | `Posicao.Infrastructure`, `Posicao.Application`, `Contratos.Integracao`, `Plataforma.Hospedagem` |
| [`Gateway`](src/Gateway/Gateway/) | Borda (serviço) | API Gateway com YARP: roteamento, bloqueio da saúde interna, limite de requisições por cliente e correlação | `Plataforma.Hospedagem` |
| [`Contratos.Integracao`](src/Compartilhado/Contratos.Integracao/) | Compartilhado | Published Language: os Integration Events `ContaAberta v1` e `MovimentacaoRegistrada v1` | nada |
| [`Plataforma.Mediator`](src/Compartilhado/Plataforma.Mediator/) | Compartilhado | Mediator dos casos de uso: `IRequest`, `IRequestHandler` e `IMediator` na raiz e a implementação em `Imp/` | nada |
| [`Plataforma.Hospedagem`](src/Compartilhado/Plataforma.Hospedagem/) | Compartilhado (só a borda usa) | Saúde dos serviços, correlação, autenticação JWT, logs e traces e o registro dos handlers no Mediator; sem conceitos de domínio | `Plataforma.Mediator` |
| [`EmissorDeTokens`](ferramentas/EmissorDeTokens/) | Ferramenta | Emite tokens JWT para o ambiente local (`docker compose run emissor-de-tokens`) | nada |

As dependências apontam sempre para o Domínio, e nenhum Bounded Context referencia o outro. Essas regras são verificadas por testes, e não apenas descritas. Os motivos estão em [openspec/changes/archive/2026-10-28-estruturar-solucao/design.md](openspec/changes/archive/2026-10-28-estruturar-solucao/design.md).

## Como o trabalho é planejado (OpenSpec)

Cada etapa é uma *change* do [OpenSpec](https://github.com/Fission-AI/OpenSpec), com proposta (por quê), specs (o que o sistema deve fazer, em cenários testáveis), design (como e quais alternativas foram descartadas) e tarefas. Ao concluir uma change, as specs dela são promovidas para `openspec/specs/`, que descreve o comportamento atual do sistema. As changes concluídas ficam em [openspec/changes/archive/](openspec/changes/archive/).
