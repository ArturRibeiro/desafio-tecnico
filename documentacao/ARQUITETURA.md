# Arquitetura e Diagramas

Como a solução responde ao enunciado do desafio. Os assuntos seguem a ordem do enunciado e terminam numa [linha do tempo](#9-linha-do-tempo).

As regras de negócio e o glossário dos termos técnicos estão em [DOMINIO.md](DOMINIO.md).

## Mapa do enunciado

| O enunciado pede | Onde está |
|---|---|
| Registrar créditos e débitos | [DOMINIO 5](DOMINIO.md#5-contexto-de-lançamentos); diagrama do [ciclo de vida de um débito](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) |
| Consultar a posição consolidada em um determinado momento | [DOMINIO 6.3](DOMINIO.md#63-cálculo-da-posição-em-um-instante) |
| Operar de forma confiável sob instabilidade, alta demanda ou falhas parciais | Seções [4](#4-consistência-concorrência-e-falhas) e [5](#5-alta-demanda-e-indisponibilidade-parcial) |
| Como estruturar a solução | Seções [1](#1-estrutura-da-solução-ddd-estratégico) e [2](#2-estrutura-da-solução-ddd-tático) |
| Quais tecnologias, frameworks e padrões, e por quê | Seção [3](#3-tecnologias-frameworks-e-padrões) |
| Como tratar consistência, concorrência e cenários de falha | Seção [4](#4-consistência-concorrência-e-falhas) |
| Como se comporta sob alta demanda ou indisponibilidade parcial | Seção [5](#5-alta-demanda-e-indisponibilidade-parcial), com o [teste de carga](#resultados-do-teste-de-carga) |
| Como proteger dados sensíveis | Seção [6](#6-proteção-de-dados-sensíveis) |
| O que documentar para o time entender e evoluir a solução | Seção [7](#7-o-que-está-documentado) |
| Trade-offs e alternativas consideradas | Seção [8](#8-trade-offs-e-alternativas) e [DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas) |
| O que faria diferente com mais tempo | Seção [9.3](#93-o-que-seria-feito-com-mais-tempo) |

Requisitos obrigatórios:

| Requisito | Como é atendido |
|---|---|
| Implementação em C# | C# 14 e .NET 10 |
| Testes automatizados | `dotnet test -c Release`: unidade, arquitetura e cenários de negócio ([DESENVOLVIMENTO](DESENVOLVIMENTO.md#testes)) |
| Repositório público no GitHub | [ArturRibeiro/desafio-tecnico](https://github.com/ArturRibeiro/desafio-tecnico) |
| README com instruções para rodar localmente | [README.md](../README.md) |
| Documentação no próprio repositório | Seção [7](#7-o-que-está-documentado) |
| Compilar sem erros e warnings | Todo warning é erro no build, inclusive no CI ([spec](../openspec/specs/qualidade-de-build/spec.md)) |

## Como ler a coluna "No desafio"

| Valor | Significado |
|---|---|
| **Implementado** | Está no código, com teste automatizado. Nos fluxos de negócio, o teste é um cenário da spec executado pelo `Scenarios.Integration.Specs.Tests` contra os serviços, o PostgreSQL, o RabbitMQ e o Redis reais. |
| **Implementado, sem teste automatizado** | Está no código e é verificado no ambiente local. |
| **Medido** | Está implementado e passou pelo [teste de carga](#resultados-do-teste-de-carga). |
| **Documentar** | Descrito como seria em produção, sem implementação. |

---

## 1. Estrutura da solução: DDD Estratégico

O problema visto de fora. Pasta [`diagramas/01-ddd-estrategico/`](diagramas/01-ddd-estrategico/)

| # | Diagrama | O que mostra |
|---|---|---|
| 1.1 | [Subdomains](diagramas/01-ddd-estrategico/01-subdomains.md) | As áreas do negócio: Core, Supporting e Generic |
| 1.2 | [Context Map](diagramas/01-ddd-estrategico/02-context-map.md) | Como os Bounded Contexts se relacionam |
| 1.3 | [C4 — System Context](diagramas/01-ddd-estrategico/03-c4-system-context.md) | O sistema, seus usuários e os sistemas externos |
| 1.4 | [C4 — Container](diagramas/01-ddd-estrategico/04-c4-container.md) | Um microserviço por Bounded Context, com bancos e broker |

## 2. Estrutura da solução: DDD Tático

O interior de cada Bounded Context. Pasta [`diagramas/02-ddd-tatico/`](diagramas/02-ddd-tatico/)

| # | Diagrama | O que mostra |
|---|---|---|
| 2.1 | [BC Lançamentos — Modelo de domínio](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md) | Aggregate Roots `Conta` e `Lancamento`, Value Objects e Domain Events |
| 2.2 | [BC Lançamentos — C4 Component](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md) | Camadas da API, com o Outbox do MassTransit |
| 2.3 | [BC Posição Consolidada — Modelo de domínio](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) | Aggregate Roots `FechamentoDiario`, `Titularidade` e `Movimento` e Value Objects |
| 2.4 | [BC Posição Consolidada — C4 Component](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Consumo de eventos e consultas |
| 2.5 | [Ciclo de vida de um débito](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) | Do comando até a posição consolidada, passando pelos dois Bounded Contexts |

---

## 3. Tecnologias, frameworks e padrões

O enunciado só exige C#. Na coluna de alternativas, `—` indica que não houve comparação registrada.

### 3.1 Tecnologias e frameworks

| Escolha | Papel e motivo | Alternativas descartadas |
|---|---|---|
| C# 14 e .NET 10 | Exigência do enunciado. | — |
| ASP.NET Core (Minimal API) e Worker Service | APIs HTTP e consumo de eventos em processos separados, que escalam e falham de forma independente. | Consumer hospedado na própria API |
| PostgreSQL 16, um banco por Bounded Context | A mesma transação grava o lançamento e o Outbox, e a reserva da linha faz as operações de uma conta correrem uma de cada vez. Um contexto não lê o banco do outro. | — |
| EF Core 10 (Npgsql) | Acesso a dados em LINQ e integração com o Outbox e o Inbox do MassTransit. | — |
| RabbitMQ 3.13 com MassTransit 8 | Outbox, Inbox, retentativas e DLQ já testados, com menos código próprio numa parte crítica. Fica na v8 (Apache 2.0), porque a v9 tem licença comercial. | Mecanismos escritos à mão; `RabbitMQ.Client` puro; MassTransit v9; Wolverine; Rebus |
| Redis 7 | Cache do saldo de abertura nas consultas. É opcional: fora do ar, a consulta vai ao banco. | — |
| YARP 2.3 | Uma única entrada pública, com limite de requisições e tempo máximo de resposta. | — |
| JWT Bearer | Cada serviço valida o token e os escopos, sem depender do gateway nem guardar senhas. | — |
| Logs JSON e OpenTelemetry (Jaeger no ambiente local) | Um único trace do gateway ao consumo no Worker, mesmo com a entrega adiada pelo Outbox. | — |
| xUnit v3, NetArchTest e FluentAssertions 7 | Testes de unidade e regras de arquitetura que falham no build. FluentAssertions fica na v7, porque a v8 tem licença comercial. | FluentAssertions v8 |
| SpecFlow, xUnit v2 e Testcontainers | Cenários das specs de negócio contra os serviços e a infraestrutura reais. Risco aceito: o SpecFlow foi descontinuado e não declara suporte ao .NET 10. | `Reqnroll.xUnit.v3`; `SpecFlow.NUnit` |
| k6 em container | Teste de carga que só precisa do Docker, sem misturar carga com a solução. | NBomber |
| Docker Compose e GitHub Actions | Ambiente local com um comando e CI. Os health checks e as APIs sem estado já estão prontos para um orquestrador. | — |

### 3.2 Padrões

| Padrão | Problema que resolve | Detalhes |
|---|---|---|
| DDD, com um microserviço por Bounded Context | Lançamentos e Posição têm cargas, consistência e significado de "Conta" diferentes. | Seções [1](#1-estrutura-da-solução-ddd-estratégico) e [8](#8-trade-offs-e-alternativas) |
| Published Language, sem Shared Kernel | Cada contexto evolui de forma independente; os eventos têm versão. | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) |
| Transactional Outbox e Inbox | Evento perdido ao gravar o lançamento e entrega repetida de eventos. | Seção [4](#4-consistência-concorrência-e-falhas) |
| Idempotência por chave | Cliente que repete a requisição não duplica lançamento. | Seção [4](#4-consistência-concorrência-e-falhas) |
| Retentativas e fila `_error` (DLQ) | Mensagem que sempre falha não trava as demais. | Seção [4](#4-consistência-concorrência-e-falhas) |
| Separação de escrita e leitura | A escrita passa por Commands e Repositories; a leitura, por Queries sobre o `IDbContextReadOnly`. | [DESENVOLVIMENTO](DESENVOLVIMENTO.md#estrutura) |
| Mediator próprio, sem dependências | Api e Worker chamam os casos de uso só pelo `IMediator`. | [DESENVOLVIMENTO](DESENVOLVIMENTO.md#estrutura) |
| Rate limiting e timeout na borda | Um cliente abusivo ou um serviço lento não afeta os demais. | Seção [5](#5-alta-demanda-e-indisponibilidade-parcial) |

---

## 4. Consistência, concorrência e falhas

Cada cenário, o que responde por ele e onde está documentado.

| Cenário | Mecanismo | Onde | No desafio |
|---|---|---|---|
| Dois débitos simultâneos na mesma conta | A `Conta` é carregada com `SELECT ... FOR UPDATE`: operações na mesma conta correm uma de cada vez, e cada uma vê o saldo atualizado. A `Versao` (concorrência otimista) é uma proteção extra. Testado com 10 débitos simultâneos: só os que cabem no saldo são aceitos. | [DOMINIO 5.1](DOMINIO.md#51-aggregate-root-conta), [2.1](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md) | Implementado: cenário [`Débitos concorrentes`](../tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature) |
| Cliente repete a requisição (timeout, nova tentativa) | `ChaveIdempotencia` obrigatória, com índice único no banco. A repetição devolve o resultado original, sem criar outro lançamento. | [2.5](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) | Implementado: cenário [`Repetição após timeout`](../tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature) |
| Lançamento gravado, mas evento não publicado | Transactional Outbox do MassTransit: lançamento e mensagem são gravados na mesma transação. | [2.2](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md) | Implementado: cenário [`Falha ao confirmar a transação`](../tests/Scenarios.Integration.Specs.Tests/Features/EventosDeIntegracao.feature) |
| Evento entregue mais de uma vez | Inbox do MassTransit (por `eventoId`) e, no banco de Posição, a chave `(conta, sequencia)`: a mesma sequência é aplicada uma única vez. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Evento repetido`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Eventos fora de ordem | `Sequencia` por conta. Se faltar um evento, o seguinte fica pendente, sem nova tentativa, e é aplicado assim que o anterior chega. Um lock por conta faz os eventos da mesma conta correrem um de cada vez. | [DOMINIO 6.2](DOMINIO.md#62-movimentos-projetados) | Implementado: cenário [`Evento fora de ordem`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Mensagem que sempre falha | Novas tentativas com intervalo crescente e, esgotadas, fila `_error` (DLQ), sem travar as demais. Um evento inválido vai direto para a `_error`. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Evento inválido`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Posição consolidada errada ou corrompida | Lançamentos não mudam: o comando `reconstruir` do `Posicao.Worker` recalcula os fechamentos a partir dos movimentos recebidos. Recuperar eventos que nunca chegaram (reenvio por Lançamentos) fica como evolução. | [DOMINIO 6.6](DOMINIO.md#66-reconstrução) | Implementado: cenário [`Fechamentos corrompidos`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Saldo de Lançamentos diferente da posição consolidada | Conciliação contínua: cada `MovimentacaoRegistrada` traz o saldo após a movimentação, e a Posição o compara com o que calculou na mesma sequência, inclusive na reconstrução. Uma diferença gera log de erro `saldo-divergente` (sem valores) e a métrica `posicao.conciliacao.divergencias`, sem parar a projeção. | [DOMINIO 6.8](DOMINIO.md#68-conciliação-com-lançamentos) | Implementado: cenário [`Saldo divergente`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |

---

## 5. Alta demanda e indisponibilidade parcial

### 5.1 Alta demanda

| Cenário | Mecanismo | Onde | No desafio |
|---|---|---|---|
| Pico de escritas | APIs sem estado, que escalam com mais instâncias. A `Conta` não carrega o histórico, então cada operação lê e grava pouco. Só operações da mesma conta esperam umas pelas outras. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md), [DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas) | Medido: [teste de carga](#resultados-do-teste-de-carga) |
| Pico de consultas | `Posicao.Api` escala separada da escrita. A posição é o fechamento do dia anterior somado aos movimentos do dia, sem percorrer todo o histórico. Saldos de abertura definitivos vêm do cache. | [DOMINIO 6.3](DOMINIO.md#63-cálculo-da-posição-em-um-instante) | Medido: [teste de carga](#resultados-do-teste-de-carga) |
| Rajada de eventos | A fila absorve o pico. O `Posicao.Worker` escala pelo número de instâncias e pela concorrência do consumer (`Consumo__LimiteDeMensagensConcorrentes`). | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Medido: [teste de carga](#resultados-do-teste-de-carga); a entrega do Outbox, gargalo da primeira medição, foi corrigida e medida de novo |
| Cliente abusivo ou sobrecarga | Limite de requisições por cliente no API Gateway (janela fixa por titular de token válido, ou por IP), com resposta `429` e `Retry-After`. O limite vale por instância do gateway. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado, sem teste automatizado |
| Crescimento do volume de dados | Particionamento da tabela de lançamentos por data e réplicas de leitura para a posição consolidada. | — | Documentar |

#### Resultados do teste de carga

Medição de 04/11/2026 com [k6](https://k6.io/), num notebook com Intel Core i5-6400 (4 núcleos), 15 GB de memória e Docker 29.9.2, com todos os containers e o k6 na mesma máquina. Para repetir: `carga/executar.sh`, com o ambiente no ar ([DESENVOLVIMENTO](DESENVOLVIMENTO.md#teste-de-carga)). Tabela e resumos brutos do k6 em [carga/resultados/](../carga/resultados/resultados.md).

| Cenário | Usuários virtuais | Duração | Vazão (req/s) | Latência p50 | Latência p95 | Erros (5xx) | Observação |
|---|---|---|---|---|---|---|---|
| Escrita em muitas contas | 20 | 52 s | 56.6 | 13 ms | 85 ms | 0 | 2930 créditos e débitos em 20 contas; 0 respostas 429 |
| Consistência ao final da escrita | 20 | 2 s | — | 1894 ms | 1895 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1895 ms |
| Débitos concorrentes na mesma conta | 60 | 1 s | 74.5 | 667 ms | 761 ms | 0 | 30 aceitos e 30 recusados por saldo, de 60 simultâneos (esperados 30 aceitos); saldo final correto. Serializados de propósito |
| Leitura junto com escrita | 50 | 52 s | 142.8 | 11 ms | 48 ms | 0 | latência e vazão das consultas, com 40 VUs de leitura e 10 de escrita; escrita no mesmo período: p95 246 ms |
| Consistência ao final da leitura | 20 | 1 s | — | 1061 ms | 1062 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1063 ms |
| Escrita com o consolidado parado | 20 | 51 s | 56.6 | 23 ms | 77 ms | 0 | 2862 créditos e débitos em 20 contas; 0 respostas 429 |
| Alcance depois de religar o consolidado | 20 | 21 s | — | 21 s | 21 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 21 s |

**Os números servem de referência e para achar o gargalo, não como capacidade de produção.** O enunciado não define metas, e uma só máquina com todos os serviços disputando 4 núcleos não representa produção.

O que a medição mostra:

- **Lançamentos responde sob carga e não depende da Posição.** A escrita fica com p95 abaixo de 90 ms, sem `5xx` nem `429`, e não muda com o `posicao-worker` parado.
- **Os débitos concorrentes ficam corretos.** Dos 60 simultâneos numa conta com saldo para 30, exatamente 30 são aceitos, e o saldo final é zero. A latência maior é a espera na fila do `SELECT ... FOR UPDATE`, como esperado.
- **A consulta mantém a latência com escrita em paralelo** (p95 abaixo de 50 ms). A escrita no mesmo período teve p95 de 246 ms nessa execução. Duas repetições só desse cenário, fora da evidência versionada, deram 76 e 86 ms, o que aponta para um pico isolado.
- **Nenhuma divergência de saldo** nas três verificações: em cada conta da amostra, a posição, ao aplicar a última sequência, tem o mesmo saldo de Lançamentos.
- **A posição acompanha a escrita.** Ela alcança em menos de 2 s depois da carga e em 21 s depois de religar o consolidado, com quase 2.900 operações acumuladas.

**Gargalo encontrado e resolvido: a entrega do Outbox de Lançamentos.** Na primeira medição, cerca de 2.800 operações em 50 s levaram uns 22 minutos para chegar à posição, porque o Outbox entregava perto de 2 eventos por segundo ao RabbitMQ.

A causa era o isolamento `RepeatableRead`, padrão da entrega do MassTransit: ciclos de entrega simultâneos falhavam (`40001`, 87 erros numa escrita de 20 s) e esperavam de 7 s a 30 s para tentar de novo. Como cada mensagem já é reservada com exclusividade (`FOR UPDATE SKIP LOCKED`), a correção foi usar `ReadCommitted` na entrega, sem mudar as garantias nem o intervalo de varredura.

Comparação com os mesmos cenários, parâmetros e máquina ([resultados de antes](../carga/resultados/antes-ajuste-do-outbox/resultados.md) e [de depois](../carga/resultados/resultados.md)):

| Medida | Antes (03/11) | Depois (04/11) |
|---|---|---|
| Posição alcança depois da escrita | 1319 s | 1,9 s |
| Posição alcança depois da leitura com escrita | 725 s | 1,1 s |
| Posição alcança depois de religar o consolidado | 1322 s | 21 s |
| Escrita em muitas contas: vazão e p95 | 55,9 req/s e 86 ms | 56,6 req/s e 85 ms |
| Escrita com o consolidado parado: vazão e p95 | 56,1 req/s e 85 ms | 56,6 req/s e 77 ms |
| Erros `40001` no log de `lancamentos-api` durante a carga | cerca de 1.500 em 3 horas de carga | 0 |

Depois da correção, não há erros `5xx` nem divergências, e o Outbox termina vazio. A vazão de consultas da linha de leitura (73,8 para 142,8 req/s) **não é ganho**: ela é calculada sobre a duração total, e antes o preparo do cenário gastava uns 45 s esperando a projeção. O número de consultas é quase o mesmo (7.131 e 7.409).

### 5.2 Indisponibilidade parcial

| Cenário | Comportamento | Onde | No desafio |
|---|---|---|---|
| RabbitMQ fora | Lançamentos continua aceitando operações. As mensagens ficam no Outbox e são entregues quando o broker volta. A posição atrasa, e o atraso aparece em `atualizadoAte`. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado: cenário [`Broker fora durante as operações`](../tests/Scenarios.Integration.Specs.Tests/Features/EventosDeIntegracao.feature) |
| Posição Consolidada fora | Lançamentos não é afetado, porque um serviço não chama o outro. Ao voltar, o Worker consome o que ficou na fila. | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) | Implementado: cenário [`Worker volta depois de parado`](../tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Redis fora | O cache é opcional: a consulta vai direto ao banco, mais lenta mas correta. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Cache fora do ar`](../tests/Scenarios.Integration.Specs.Tests/Features/ConsultaDePosicao.feature) |
| Banco de Lançamentos fora | A operação falha sem gravar nada (`500`, sem detalhes internos), e a prontidão responde `503`, tirando a instância do balanceamento. Com dinheiro, é melhor recusar do que perder. | — | Implementado: cenário [`Falha inesperada`](../tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature); a resposta `503` da prontidão não tem teste automatizado |
| Instância com problema | Health checks (`/health/live`, `/health/ready`) para o orquestrador retirar a instância do balanceamento. | — | Implementado, sem teste automatizado |
| Serviço interno lento | Tempo máximo de 10 segundos por cluster no API Gateway (`ActivityTimeout` do YARP), com resposta `504`, para a lentidão de um serviço não esgotar os recursos do gateway. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado, sem teste automatizado |
| Serviço interno falhando seguidamente | Health check passivo do YARP: o destino com falhas seguidas é marcado como indisponível, e o gateway responde `503` na hora, sem chamá-lo, até ele se recuperar (efeito de circuit breaker). | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Documentar |

### 5.3 Observabilidade

Sem observabilidade, os mecanismos acima falham sem ninguém perceber.

| O que | Como | No desafio |
|---|---|---|
| Rastrear uma operação de ponta a ponta | `correlacaoId` propagado da requisição até o evento e um único trace OpenTelemetry do gateway ao consumo no Worker, mesmo com a entrega adiada pelo Outbox. No ambiente local, os traces vão para o Jaeger. | Implementado, sem teste automatizado |
| Logs | Em JSON, com a correlação e o trace em cada registro, sem token, titular, valores nem descrições. | Implementado, sem teste automatizado |
| Saúde da integração | Métricas de mensagens pendentes no Outbox, tamanho da fila, mensagens na `_error` e atraso da posição (`agora - atualizadoAte`), com alertas. | Documentar |

---

## 6. Proteção de dados sensíveis

O enunciado trata de dados financeiros sensíveis. A proteção começa por guardar o **mínimo** de dados e segue com controles em camadas.

### 6.1 Autenticação e autorização

| Controle | Como |
|---|---|
| Autenticação | OIDC com o Provedor de Identidade; o sistema não guarda senhas. Tokens JWT de vida curta, validados pela chave pública (JWKS). |
| Validação em cada serviço | Cada serviço valida o token e confere os escopos; nenhum confia só por estar atrás do gateway. O API Gateway também valida o token, mas só para identificar o cliente no limite de requisições: um token inválido segue para o serviço, que responde `401`. |
| Permissões separadas | Escopos `lancamentos:escrever` e `posicao:ler`, conferidos em cada endpoint ([Premissas](DOMINIO.md#premissas)). |
| Dono da conta | Cada conta guarda o seu `TitularId`, o identificador do cliente no Provedor de Identidade (claim `sub`). Ele vem sempre do token, nunca da requisição. Lançamentos confere pela `Conta` ([DOMINIO 5.6](DOMINIO.md#56-verificação-de-titularidade)); Posição Consolidada, pela `Titularidade` recebida em `ContaAberta v1` ([DOMINIO 6.7](DOMINIO.md#67-verificação-de-titularidade)), sem chamar Lançamentos. Conta inexistente ou de outro titular responde `404`, para não revelar que a conta existe. |
| Comunicação entre serviços | Serviços, bancos e broker numa rede privada, sem acesso externo; só o API Gateway é público. Em produção, mTLS entre serviços. |

### 6.2 Proteção dos dados

| Controle | Como |
|---|---|
| Mínimo de dados | O sistema guarda só identificadores, valores e datas. Nome, documento e outros dados cadastrais ficam fora do escopo ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)). Os eventos não levam dados cadastrais; o `TitularId` trafega uma única vez, em `ContaAberta v1`. |
| Em trânsito | Em produção, HTTPS (TLS 1.2+) do cliente até a borda, com o TLS terminando no ingress ou load balancer à frente do gateway, e TLS também para PostgreSQL, RabbitMQ e Redis. No ambiente local, tudo trafega em HTTP. |
| Em repouso | Criptografia de disco e backups nos bancos gerenciados. O campo `descricao` é texto livre, o único que pode conter dado sensível, e é candidato a criptografia em nível de coluna. |
| Segredos | Nenhum segredo no repositório. No ambiente local, variáveis de ambiente vindas de arquivos com valores falsos; em produção, um cofre de segredos (Azure Key Vault, AWS Secrets Manager ou HashiCorp Vault) com rotação. |
| Logs e erros | Logs sem tokens, descrição de lançamento ou dados pessoais. Respostas de erro no formato Problem Details (RFC 7807), sem stack trace. |
| Cache | O Redis guarda só posições de dias fechados, indexadas por `contaId`, com expiração. |

### 6.3 Integridade e auditoria

| Controle | Como |
|---|---|
| Trilha de auditoria | Lançamentos são só incluídos; correções são estornos. O histórico completo é a própria auditoria. |
| Quem fez o quê | Cada lançamento fica gravado com a conta (e, por ela, o titular), o instante de registro e a chave de idempotência. Os logs ligam a operação pelo `correlacaoId` e pelo trace, sem o titular. Em produção, um log de auditoria separado, com acesso restrito, registraria também o cliente autenticado. |
| Privilégio mínimo no banco | O usuário da aplicação não tem permissão de `UPDATE` nem `DELETE` na tabela de lançamentos. |
| Entrada de dados | Validação na borda (valores positivos, tamanho máximo de `descricao`, formato da `ChaveIdempotencia`) e consultas sempre parametrizadas (EF Core). |
| Dependências | O NuGet Audit verifica vulnerabilidades conhecidas dos pacotes a cada restauração. Como todo warning é erro, um pacote vulnerável quebra o build, inclusive no CI. |

### 6.4 No desafio

**No código:** validação JWT nos serviços, escopos, verificação do dono da conta, limite de requisições, segredos por variáveis de ambiente, logs sem dados sensíveis, Problem Details, validação de entrada e auditoria de pacotes no build. Para rodar localmente, um emissor de tokens de desenvolvimento substitui o Provedor de Identidade.

**Documentado como evolução para produção:** TLS na borda (o ambiente local usa HTTP por simplicidade), TLS interno, mTLS, cofre de segredos, criptografia em nível de coluna e privilégio mínimo no banco.

---

## 7. O que está documentado

O essencial para outra pessoa do time entender e evoluir a solução, tudo no repositório:

| Para saber | Leia |
|---|---|
| Como rodar localmente | [README.md](../README.md) |
| Por que a solução é assim | Este documento |
| Regras de negócio, termos e decisões de modelagem | [DOMINIO.md](DOMINIO.md) |
| API, eventos, testes, teste de carga, estrutura e convenções | [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md) |
| A estrutura e os fluxos em imagens | [diagramas/](diagramas/) |
| O que o sistema deve fazer, em cenários testáveis | [openspec/specs/](../openspec/specs/) |
| Por que cada etapa foi feita e o que foi descartado | [openspec/changes/archive/](../openspec/changes/archive/) |
| Os números medidos sob carga | [carga/resultados/](../carga/resultados/resultados.md) |

### Legenda dos diagramas C4

Os diagramas são escritos em Mermaid e exibidos direto pelo GitHub. Eles seguem a notação visual do C4 (cores, tipos de elemento e tecnologia entre colchetes), mas usam `flowchart`, porque a sintaxe `C4Context` do Mermaid ainda é experimental e gera layouts difíceis de ler.

| Cor | Elemento |
|---|---|
| Azul escuro | Pessoa |
| Azul | Sistema em escopo |
| Cinza | Sistema externo |
| Azul médio | Container (aplicação, banco de dados, fila) |
| Azul claro | Component |
| Borda tracejada | Fronteira (sistema, Bounded Context ou container) |
| BC | Bounded Context |

### Relação entre DDD e C4

| Conceito de DDD | Nível | Onde aparece |
|---|---|---|
| Subdomain (Core, Supporting, Generic) | Estratégico | [1.1](diagramas/01-ddd-estrategico/01-subdomains.md) e o Provedor de Identidade como sistema externo |
| Bounded Context | Estratégico | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) e fronteiras no [1.4](diagramas/01-ddd-estrategico/04-c4-container.md): um microserviço por contexto |
| Upstream / Downstream | Estratégico | Direção do fluxo Lançamentos → RabbitMQ → Posição Consolidada |
| Published Language | Estratégico | Eventos `ContaAberta v1` e `MovimentacaoRegistrada v1` trafegando pelo broker |
| Sem Shared Kernel | Estratégico | Bancos separados e Value Objects duplicados em cada contexto |
| Aggregate Root, Value Object, Repository | Tático | [2.1](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md), [2.3](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) e o componente **Modelo de Domínio** |
| Domain Event → Integration Event | Tático | Tradutor de Eventos e Outbox do MassTransit ([2.2](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md)) e o ciclo de vida ([2.5](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md)) |

---

## 8. Trade-offs e alternativas

Toda flexibilidade cobra complexidade. A regra foi pagar esse custo só onde o enunciado exige e manter simples o resto.

| Escolha | O que ganha | O que custa | Por que vale aqui |
|---|---|---|---|
| Dois microsserviços, um por Bounded Context | Lançamentos e Posição escalam, são publicados e falham de forma independente | Broker, dois bancos, consistência eventual e mais peças para operar | O enunciado exige que o sistema continue funcionando em falhas parciais. Um monólito modular seria mais simples, mas uma falha ou um pico da consulta afetaria a escrita. Dividir mais (um serviço por caso de uso) só somaria custo |
| Consistência eventual na posição | Falhas isoladas e escrita rápida, sem esperar a projeção | A posição pode ficar alguns segundos atrasada | O atraso aparece em `atualizadoAte`, e o saldo que autoriza débitos fica em Lançamentos, com consistência forte ([DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas)) |
| MassTransit sobre RabbitMQ | Outbox, Inbox, retentativas e DLQ já testados | Uma dependência com suas convenções e, na v9, licença comercial | Escrever isso à mão, numa parte crítica, seria mais código e mais risco |
| API Gateway (YARP) | Uma única entrada pública, com limite de requisições e tempo máximo de resposta | Mais um salto de rede e mais um componente | É o ponto natural para proteger a entrada e retirar destinos com falha |
| Cache Redis opcional | Consultas mais rápidas nos picos | Mais um componente e a limpeza na reconstrução | Fora do ar, a consulta vai ao banco, mais lenta mas correta |
| DDD tático, camadas e testes de arquitetura | Regras de dinheiro isoladas, testáveis e protegidas contra regressões | Mais arquivos e mais cerimônia que um CRUD | O risco está em saldo errado ou lançamento perdido; vale isolar essas regras |

O que ficou simples de propósito, e como evoluir:

- **Entrega do Outbox dentro da `Lancamentos.Api`**, sem um worker dedicado. Mover para um worker é só configuração, se o volume pedir.
- **Limite de requisições por instância do gateway.** Com várias instâncias, o limite real se multiplica. Um limite distribuído (Redis) entra quando o gateway tiver mais instâncias.
- **Emissor de tokens de desenvolvimento** no lugar do Provedor de Identidade, só para rodar localmente.
- **`docker compose`** como ambiente, sem orquestrador. Os health checks e as APIs sem estado já estão prontos para um orquestrador com autoscaling.

As demais decisões de modelagem, com as alternativas, estão no [DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas).

---

## 9. Linha do tempo

Do que já aconteceu ao que viria depois. As datas vêm dos registros de cada etapa no OpenSpec e do histórico do Git.

### 9.1 Como a solução foi construída

A ordem seguiu o risco: a base dos requisitos obrigatórios, depois o dinheiro, a consulta, a proteção e a prova. Os períodos vão do início da etapa ao seu arquivamento e por isso se sobrepõem.

| Período | Etapa | O que trouxe |
|---|---|---|
| 28/10 | [Estruturar a solução](../openspec/changes/archive/2026-10-28-estruturar-solucao/) | Base dos requisitos obrigatórios: build sem warnings, testes de arquitetura, gateway, saúde dos serviços, ambiente local e CI, ainda sem regra de negócio. |
| 28/10 a 29/10 | [BC Lançamentos](../openspec/changes/archive/2026-10-29-implementar-bc-lancamentos/) | O mais crítico, a fonte da verdade: créditos, débitos, estornos, idempotência, concorrência por conta e Outbox. |
| 29/10 | [BC Posição Consolidada](../openspec/changes/archive/2026-10-29-implementar-bc-posicao-consolidada/) | Projeção dos eventos, posição em um instante, fechamentos diários, cache e reconstrução. |
| 29/10 | [Proteger e observar o gateway](../openspec/changes/archive/2026-10-29-proteger-e-observar-o-gateway/) | Limite de requisições, logs em JSON e traces de ponta a ponta. |
| 29/10 a 02/11 | [Cenários em linguagem de negócio](../openspec/changes/archive/2026-11-02-cenarios-em-linguagem-de-negocio/) | Cenários que descrevem o negócio, e não rotas nem infraestrutura. |
| 29/10 a 03/11 | [Executar os cenários das specs](../openspec/changes/archive/2026-11-03-executar-cenarios-das-specs/) | Os fluxos de negócio voltaram a ter verificação de ponta a ponta, com serviços e infraestrutura reais. |
| 03/11 | [Conciliar o saldo na projeção](../openspec/changes/archive/2026-11-03-conciliar-saldo-na-projecao/) | Revisão contra o enunciado: uma diferença de saldo entre os contextos deixou de passar sem aviso. |
| 03/11 | Convenções de código | Convenções garantidas por testes de arquitetura, incluindo todo o acesso a dados em LINQ. |
| 03/11 | [Teste de carga](../openspec/changes/archive/2026-11-04-teste-de-carga/) | Alta demanda deixou de estar só no desenho. A primeira medição mostrou a posição 22 minutos atrás da escrita. |
| 03/11 a 04/11 | [Ajustar a entrega do Outbox](../openspec/changes/archive/2026-11-04-ajustar-entrega-do-outbox/) | Causa confirmada, corrigida e medida de novo: o alcance caiu para menos de 2 s. |

### 9.2 Desafios enfrentados

Na ordem das etapas em que apareceram.

| Desafio | Como foi resolvido |
|---|---|
| **Licença do MassTransit.** A v9 passou a ter licença comercial. | Fixado na v8 (Apache 2.0), com a versão verificada por teste. A decisão fica registrada para reavaliação em produção. |
| **Gravar o lançamento e publicar o evento sem perder nenhum dos dois.** | Transactional Outbox: lançamento e mensagem na mesma transação, com entrega quando o broker estiver disponível. |
| **Débitos simultâneos na mesma conta.** Só a concorrência otimista gera novas tentativas justamente no caso mais disputado. | Reserva da linha da `Conta` em cada operação, com a `Versao` como proteção extra. O cenário com 10 débitos simultâneos prova que só os que cabem no saldo são aceitos. |
| **Eventos fora de ordem e repetidos.** O broker não garante ordem, e a posição depende dela. | `Sequencia` por conta: se faltar um evento, o seguinte fica pendente até o anterior chegar. A chave `(conta, sequencia)` e o Inbox descartam repetições, e um lock por conta faz a projeção correr um evento de cada vez ([4](#4-consistência-concorrência-e-falhas)). |
| **Detectar diferença de saldo entre os contextos sem ligar um ao outro.** A primeira versão só documentava uma conciliação periódica entre os bancos. | O evento passou a levar o saldo após a movimentação, e a Posição compara na mesma sequência. A diferença é detectada no movimento que a causou, sem um serviço chamar o outro nem ler o banco dele ([DOMINIO 6.8](DOMINIO.md#68-conciliação-com-lançamentos)). |
| **Manter todo acesso a dados em LINQ.** `SELECT ... FOR UPDATE` e o lock por conta não têm equivalente em LINQ, e uma soma sobre o Value Object `Valor` não era traduzida. | O `FOR UPDATE` entra por `TagWith` e um interceptor do EF Core, o lock por conta virou uma função do banco mapeada com `HasDbFunction`, e a soma usa a coluna por trás do conversor. Cada tradução foi conferida no SQL gerado. |
| **Fazer as convenções valerem com o tempo.** Uma regra só documentada se perde. | Um teste de arquitetura para cada convenção, e cada regra provada com um teste de mutação (o código que a viola precisa fazer o teste falhar). Assim apareceu que o NetArchTest não enxergava o acesso a um `DbSet`: a verificação passou a ler o IL compilado. |
| **Build verde na máquina, vermelho no CI.** Uma regra genérica do `.gitignore` (`[Ll]ogs/`) ignorava uma pasta de código dos testes. | Exceção no `.gitignore` e validação a partir de um clone limpo, como o CI faz. |
| **Posição 22 minutos atrás da escrita.** O teste de carga mostrou a entrega do Outbox perto de 2 eventos por segundo, sem nenhum erro para o cliente. | A contagem de erros `40001` no log confirmou a causa antes de qualquer ajuste. O isolamento `ReadCommitted` na entrega bastou, e a nova medição comprovou ([5.1](#resultados-do-teste-de-carga)). |

### 9.3 O que seria feito com mais tempo

Em ordem de prioridade. Os itens vêm das colunas **Documentar** das seções 4 a 6 e do [DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras).

**1. Antes de produção: proteger e enxergar**

- TLS na borda (ingress ou load balancer) e para PostgreSQL, RabbitMQ e Redis; cofre de segredos com rotação ([6.2](#62-proteção-dos-dados)).
- WAF à frente do gateway (regras OWASP Top 10) e proteção contra DDoS do provedor de nuvem, somados ao limite de requisições que já existe.
- Métricas e alertas da integração: mensagens pendentes no Outbox, tamanho da fila, mensagens na `_error` e atraso da posição ([5.3](#53-observabilidade)).
- Health check passivo do YARP como circuit breaker ([5.2](#52-indisponibilidade-parcial)) e privilégio mínimo no banco ([6.3](#63-integridade-e-auditoria)).

**2. Com o crescimento: volume e operação**

- Particionamento da tabela de lançamentos por data e réplicas de leitura para a posição ([5.1](#51-alta-demanda)).
- Reenvio de eventos por Lançamentos, para reconstruir a posição mesmo depois de perder eventos ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)).
- Limite de requisições distribuído entre as instâncias do gateway; entrega do Outbox num worker dedicado.
- mTLS entre serviços, criptografia em nível de coluna da `descricao` e log de auditoria separado ([6.1](#61-autenticação-e-autorização), [6.2](#62-proteção-dos-dados), [6.3](#63-integridade-e-auditoria)).
- Reavaliar o MassTransit: licença da v9 ou migração para uma alternativa.

**3. Expansão do negócio**

- Histórico de movimentações da conta (extrato): consulta das entradas e saídas de valores, com filtro por período. O enunciado pede o registro das movimentações e a posição consolidada, não essa consulta. Hoje a API devolve só a posição e os fechamentos diários, mas os lançamentos já ficam gravados ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)).
- Partidas dobradas, base para transferências entre contas.
- Múltiplas moedas, lançamentos agendados e um contexto de Clientes ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)). A divisão em Bounded Contexts e os eventos com versão permitem acrescentá-los sem reescrever o que existe.
