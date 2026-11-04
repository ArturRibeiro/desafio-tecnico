# Arquitetura e Diagramas

Este documento é o índice dos diagramas da solução. Cada diagrama fica em um arquivo próprio na pasta [`diagramas/`](diagramas/), na ordem do DDD:

1. **DDD Estratégico**: o problema visto de fora. Subdomains, Bounded Contexts, como eles se relacionam (Context Map) e como viram sistemas e containers (C4 System Context e Container).
2. **DDD Tático**: o interior de cada Bounded Context. Aggregates, Value Objects, Domain Services, Domain Events e os componentes que os implementam (C4 Component).

Depois dos diagramas, este documento mostra como a arquitetura responde aos cenários do enunciado: [atributos de qualidade](#3-atributos-de-qualidade) (instabilidade, alta demanda e falhas parciais) e [segurança e proteção de dados](#4-segurança-e-proteção-de-dados). Por fim, discute os [trade-offs, os desafios enfrentados e a evolução](#5-trade-offs-desafios-e-evolução) da solução.

As regras de negócio e a Ubiquitous Language estão descritas em [DOMINIO.md](DOMINIO.md).

---

## 1. DDD Estratégico

Pasta [`diagramas/01-ddd-estrategico/`](diagramas/01-ddd-estrategico/)

| # | Diagrama | O que mostra |
|---|---|---|
| 1.1 | [Subdomains](diagramas/01-ddd-estrategico/01-subdomains.md) | Core, Supporting e Generic Subdomains do domínio de movimentações financeiras |
| 1.2 | [Context Map](diagramas/01-ddd-estrategico/02-context-map.md) | Relações Upstream/Downstream e padrões de integração entre os Bounded Contexts |
| 1.3 | [C4 — System Context](diagramas/01-ddd-estrategico/03-c4-system-context.md) | O sistema, seus usuários e os sistemas externos |
| 1.4 | [C4 — Container](diagramas/01-ddd-estrategico/04-c4-container.md) | Um microserviço por Bounded Context, com bancos e broker |

## 2. DDD Tático

Pasta [`diagramas/02-ddd-tatico/`](diagramas/02-ddd-tatico/)

| # | Diagrama | O que mostra |
|---|---|---|
| 2.1 | [BC Lançamentos — Modelo de domínio](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md) | Aggregate Roots `Conta` e `Lancamento`, Value Objects e Domain Events |
| 2.2 | [BC Lançamentos — C4 Component](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md) | Camadas da API, com o Outbox do MassTransit |
| 2.3 | [BC Posição Consolidada — Modelo de domínio](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) | Aggregate Roots `FechamentoDiario`, `Titularidade` e `Movimento` e Value Objects |
| 2.4 | [BC Posição Consolidada — C4 Component](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Consumo de eventos e consultas |
| 2.5 | [Ciclo de vida de um débito](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) | Do comando até a posição consolidada, passando pelos dois Bounded Contexts |

---

## 3. Atributos de qualidade

Como cada cenário do enunciado é tratado, qual mecanismo responde por ele e onde ele aparece na documentação.

A coluna **No desafio** mostra a priorização: **Implementado** está no código entregue, com o teste automatizado que o comprova. Nos fluxos de negócio, a prova é um cenário da spec executado pelo `Scenarios.Integration.Specs.Tests` contra os serviços, o PostgreSQL, o RabbitMQ e o Redis reais. **Implementado, sem teste automatizado** está no código, mas é verificado no ambiente local (são comportamentos técnicos, das specs técnicas). **Medido** está implementado e passou pelo [teste de carga](#resultados-do-teste-de-carga). **Documentar** fica descrito como seria feito em produção, sem implementação.

### 3.1 Consistência e concorrência

| Cenário | Mecanismo | Onde | No desafio |
|---|---|---|---|
| Dois débitos simultâneos na mesma conta | A `Conta` é carregada com `SELECT ... FOR UPDATE`: operações na mesma conta correm em série, e cada uma vê o saldo atualizado. A `Versao` (concorrência otimista) fica como proteção extra, e a invariante de limite é sempre verificada no Aggregate. Testado com 10 débitos simultâneos: exatamente os que cabem no saldo são aceitos. | [DOMINIO 5.1](DOMINIO.md#51-aggregate-root-conta), [2.1](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md) | Implementado: cenário [`Débitos concorrentes`](tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature) |
| Cliente repete a requisição (timeout, retry) | `ChaveIdempotencia` obrigatória, com índice único no banco. A repetição devolve o resultado original sem criar outro lançamento. | [2.5](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) | Implementado: cenário [`Repetição após timeout`](tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature) |
| Lançamento gravado, mas evento não publicado (dual write) | Transactional Outbox do MassTransit: lançamento e mensagem são gravados na mesma transação. | [2.2](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md) | Implementado: cenário [`Falha ao confirmar a transação`](tests/Scenarios.Integration.Specs.Tests/Features/EventosDeIntegracao.feature) |
| Evento entregue mais de uma vez | Inbox do MassTransit (por `eventoId`) e, no banco de Posição, a chave `(conta, sequencia)` do movimento: a mesma sequência é aplicada uma única vez. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Evento repetido`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Eventos fora de ordem | `Sequencia` por conta. Um evento com lacuna fica gravado como pendente, sem retentativa, e é aplicado assim que o anterior chega. Um lock por conta serializa os eventos da mesma conta. | [DOMINIO 6.2](DOMINIO.md#62-movimentos-projetados) | Implementado: cenário [`Evento fora de ordem`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Mensagem que sempre falha | Retentativas com intervalo crescente e, esgotadas, fila `_error` (DLQ), sem bloquear as demais. Um evento inválido vai direto para a `_error`. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Evento inválido`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Posição consolidada divergente ou corrompida | Lançamentos são imutáveis: o comando `reconstruir` do `Posicao.Worker` recalcula os fechamentos a partir dos movimentos recebidos. Recuperar eventos que nunca chegaram (reenvio por Lançamentos) fica como evolução. | [DOMINIO 6.6](DOMINIO.md#66-reconstrução) | Implementado: cenário [`Fechamentos corrompidos`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Saldo de Lançamentos diferente da posição consolidada | Conciliação contínua: cada `MovimentacaoRegistrada` traz o saldo disponível após a movimentação, e a Posição o compara com o saldo que calculou na mesma sequência, inclusive na reconstrução. Uma divergência gera log de erro `saldo-divergente` (sem valores) e a métrica `posicao.conciliacao.divergencias`, sem bloquear a projeção. | [DOMINIO 6.8](DOMINIO.md#68-conciliação-com-lançamentos) | Implementado: cenário [`Saldo divergente`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |

### 3.2 Indisponibilidade parcial

| Cenário | Comportamento | Onde | No desafio |
|---|---|---|---|
| RabbitMQ fora | Lançamentos continua aceitando operações; as mensagens acumulam no Outbox e são entregues quando o broker volta. A posição fica atrasada, e o atraso aparece em `atualizadoAte`. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado: cenário [`Broker fora durante as operações`](tests/Scenarios.Integration.Specs.Tests/Features/EventosDeIntegracao.feature) |
| Posição Consolidada fora | Lançamentos não é afetado (não há chamada síncrona entre os contextos). Ao voltar, o Worker consome o que ficou na fila. | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) | Implementado: cenário [`Worker volta depois de parado`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Redis fora | O cache é opcional: a consulta vai direto ao banco, mais lenta mas correta. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Cache fora do ar`](tests/Scenarios.Integration.Specs.Tests/Features/ConsultaDePosicao.feature) |
| Banco de Lançamentos fora | A operação falha sem gravar nada (`500`, sem detalhes internos), e a prontidão responde `503`, tirando a instância do balanceamento. Em dinheiro, é melhor recusar do que perder. | — | Implementado: cenário [`Falha inesperada`](tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature); a resposta `503` da prontidão não tem teste automatizado |
| Instância com problema | Health checks (`/health/live`, `/health/ready`) para o orquestrador retirar a instância do balanceamento. | — | Implementado, sem teste automatizado |
| Serviço interno lento | Timeout de 10 segundos por cluster no API Gateway (`ActivityTimeout` do YARP), respondendo `504`, para a lentidão de um serviço não esgotar os recursos do gateway. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado, sem teste automatizado |
| Serviço interno falhando seguidamente | Health check passivo do YARP: o destino com falhas seguidas é marcado como indisponível e o gateway responde `503` na hora, sem chamá-lo, até ele se recuperar (efeito de circuit breaker). | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Documentar |

### 3.3 Alta demanda

| Cenário | Mecanismo | Onde | No desafio |
|---|---|---|---|
| Pico de escritas | APIs sem estado, escaláveis horizontalmente. A `Conta` não carrega o histórico, então cada operação lê e grava pouco; a disputa existe só entre operações da mesma conta. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md), [DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas) | Medido: [teste de carga](#resultados-do-teste-de-carga) |
| Pico de consultas | `Posicao.Api` escala separada da escrita. A posição é o fechamento do dia anterior somado aos movimentos do dia, sem varrer o histórico; saldos de abertura definitivos vêm do cache. | [DOMINIO 6.3](DOMINIO.md#63-cálculo-da-posição-em-um-instante) | Medido: [teste de carga](#resultados-do-teste-de-carga) |
| Rajada de eventos | A fila amortece o pico; o `Posicao.Worker` escala pelo número de instâncias e pela concorrência do consumer (`Consumo__LimiteDeMensagensConcorrentes`). | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Medido: [teste de carga](#resultados-do-teste-de-carga); a entrega do Outbox, gargalo encontrado na primeira medição, foi corrigida e medida de novo |
| Cliente abusivo ou sobrecarga | Rate limiting por cliente no API Gateway (janela fixa por titular de token válido, ou por IP), respondendo `429` com `Retry-After`. O limite vale por instância do gateway. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado, sem teste automatizado |
| Crescimento do volume de dados | Particionamento da tabela de lançamentos por data e réplicas de leitura para a posição consolidada. | — | Documentar |

#### Resultados do teste de carga

Medição de 04/11/2026 com [k6](https://k6.io/), num notebook com Intel Core i5-6400 (4 núcleos), 15 GB de memória e Docker 29.9.2, com todos os containers e o próprio k6 na mesma máquina. Para reproduzir: `carga/executar.sh`, com o ambiente no ar ([DESENVOLVIMENTO](DESENVOLVIMENTO.md#teste-de-carga)). Tabela gerada pelo script e resumos brutos do k6 em [carga/resultados/](carga/resultados/resultados.md).

| Cenário | Usuários virtuais | Duração | Vazão (req/s) | Latência p50 | Latência p95 | Erros (5xx) | Observação |
|---|---|---|---|---|---|---|---|
| Escrita em muitas contas | 20 | 52 s | 56.6 | 13 ms | 85 ms | 0 | 2930 créditos e débitos em 20 contas; 0 respostas 429 |
| Consistência ao final da escrita | 20 | 2 s | — | 1894 ms | 1895 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1895 ms |
| Débitos concorrentes na mesma conta | 60 | 1 s | 74.5 | 667 ms | 761 ms | 0 | 30 aceitos e 30 recusados por saldo, de 60 simultâneos (esperados 30 aceitos); saldo final correto. Serializados de propósito |
| Leitura junto com escrita | 50 | 52 s | 142.8 | 11 ms | 48 ms | 0 | latência e vazão das consultas, com 40 VUs de leitura e 10 de escrita; escrita no mesmo período: p95 246 ms |
| Consistência ao final da leitura | 20 | 1 s | — | 1061 ms | 1062 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1063 ms |
| Escrita com o consolidado parado | 20 | 51 s | 56.6 | 23 ms | 77 ms | 0 | 2862 créditos e débitos em 20 contas; 0 respostas 429 |
| Alcance depois de religar o consolidado | 20 | 21 s | — | 21 s | 21 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 21 s |

**Os números servem de referência e para encontrar o gargalo, não como capacidade de produção.** O enunciado não define metas numéricas, e uma máquina só com todos os serviços disputando 4 núcleos não representa um ambiente de produção.

O que a medição mostra:

- **Lançamentos responde sob carga e não depende da Posição.** A escrita fica com p95 abaixo de 90 ms, sem `5xx` nem `429`, e não muda com o `posicao-worker` parado.
- **Os débitos concorrentes ficam corretos.** Dos 60 simultâneos numa conta com saldo para 30, exatamente 30 são aceitos, e o saldo final é zero. A latência maior é a fila do `SELECT ... FOR UPDATE`, esperada.
- **A consulta mantém a latência com escrita em paralelo** (p95 abaixo de 50 ms). A escrita no mesmo período teve p95 de 246 ms nessa execução; duas repetições só desse cenário, fora da evidência versionada, deram 76 e 86 ms, o que aponta para um pico isolado numa máquina compartilhada.
- **Nenhuma divergência de saldo** nas três verificações: em cada conta da amostra, a posição, ao aplicar a última sequência, tem o mesmo saldo de Lançamentos.
- **A posição acompanha a escrita.** Ela alcança em menos de 2 s depois da carga e em 21 s depois de religar o consolidado, com quase 2.900 operações acumuladas.

**Gargalo encontrado e resolvido: a entrega do Outbox de Lançamentos.** Na primeira medição, cerca de 2.800 operações em 50 s levaram uns 22 minutos para chegar à posição, porque o Outbox entregava ao RabbitMQ perto de 2 eventos por segundo. A causa era o isolamento `RepeatableRead`, padrão da entrega do MassTransit: ciclos de entrega concorrentes falhavam por serialização (`40001`, 87 erros numa escrita de 20 s) e esperavam a retentativa, de 7 s a 30 s. A reserva de cada mensagem já é exclusiva (`FOR UPDATE SKIP LOCKED`), então a correção foi usar `ReadCommitted` na entrega, sem mudar as garantias nem o intervalo de varredura.

Comparação com os mesmos cenários, parâmetros e máquina ([resultados de antes](carga/resultados/antes-ajuste-do-outbox/resultados.md) e [de depois](carga/resultados/resultados.md)):

| Medida | Antes (03/11) | Depois (04/11) |
|---|---|---|
| Posição alcança depois da escrita | 1319 s | 1,9 s |
| Posição alcança depois da leitura com escrita | 725 s | 1,1 s |
| Posição alcança depois de religar o consolidado | 1322 s | 21 s |
| Escrita em muitas contas: vazão e p95 | 55,9 req/s e 86 ms | 56,6 req/s e 85 ms |
| Escrita com o consolidado parado: vazão e p95 | 56,1 req/s e 85 ms | 56,6 req/s e 77 ms |
| Erros `40001` no log de `lancamentos-api` durante a carga | cerca de 1.500 em 3 horas de carga | 0 |

Depois da correção não há erros `5xx` nem divergências, e as mensagens pendentes no Outbox terminam em zero. A vazão de consultas da linha de leitura (73,8 para 142,8 req/s) **não é ganho**: ela é calculada sobre a duração total, e antes o preparo do cenário gastava uns 45 s esperando a projeção. O número de consultas é quase o mesmo (7.131 e 7.409).

### 3.4 Observabilidade

Sem observabilidade, os mecanismos acima falham em silêncio.

| O que | Como | No desafio |
|---|---|---|
| Rastrear uma operação ponta a ponta | `correlacaoId` propagado da requisição até o evento e um único trace OpenTelemetry do gateway ao consumo no Worker, mesmo com a entrega adiada pelo Outbox. No ambiente local, os traces vão para o Jaeger. | Implementado, sem teste automatizado |
| Logs | Estruturados (JSON), com a correlação e o trace em cada registro, sem token, titular, valores nem descrições. | Implementado, sem teste automatizado |
| Saúde da integração | Métricas de mensagens pendentes no Outbox, profundidade da fila, mensagens na `_error` e atraso da posição (`agora - atualizadoAte`), com alertas. | Documentar |

---

## 4. Segurança e proteção de dados

O enunciado trata de dados financeiros sensíveis. A proteção começa por **minimizar** o que cada parte do sistema conhece e segue com controles em camadas.

### 4.1 Autenticação e autorização

| Controle | Como |
|---|---|
| Autenticação | OIDC com o Provedor de Identidade; o sistema não armazena senhas. Tokens JWT de vida curta, validados pela chave pública (JWKS). |
| Defesa em profundidade | Cada serviço valida o token e confere os escopos; um serviço nunca confia apenas por estar atrás do gateway. O API Gateway também valida o token, mas só para identificar o cliente no rate limiting: um token inválido segue para o serviço, que responde `401`. |
| Permissões separadas | Escopos `lancamentos:escrever` e `posicao:ler`, conferidos em cada endpoint ([Premissas](DOMINIO.md#premissas)). |
| Posse da conta | Cada conta guarda o seu `TitularId`, o identificador do cliente no Provedor de Identidade (claim `sub`). Ele vem sempre do token, nunca da requisição. Lançamentos confere pela `Conta` ([DOMINIO 5.6](DOMINIO.md#56-verificação-de-titularidade)); Posição Consolidada, pela `Titularidade` projetada a partir de `ContaAberta v1` ([DOMINIO 6.7](DOMINIO.md#67-verificação-de-titularidade)), sem chamar Lançamentos. Conta inexistente ou de outro titular responde `404`, para não revelar que a conta existe. |
| Comunicação entre serviços | Serviços, bancos e broker numa rede privada, sem exposição externa; somente o API Gateway é público. Em produção, mTLS entre serviços. |

### 4.2 Proteção dos dados

| Controle | Como |
|---|---|
| Minimização | O sistema guarda apenas identificadores, valores e datas. Nome, documento e demais dados cadastrais ficam fora do escopo ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)). Os eventos não carregam dados cadastrais; o `TitularId`, pseudônimo, trafega uma única vez, em `ContaAberta v1`. |
| Em trânsito | Em produção, HTTPS (TLS 1.2+) do cliente até a borda, com o TLS terminando no ingress ou load balancer à frente do gateway, e TLS também para PostgreSQL, RabbitMQ e Redis. No ambiente local, tudo trafega em HTTP. |
| Em repouso | Criptografia de disco e backups nos bancos gerenciados. O campo `descricao` do lançamento, por ser texto livre, é o único que pode conter dado sensível e é candidato a criptografia em nível de coluna. |
| Segredos | Nenhum segredo no repositório. Localmente, variáveis de ambiente vindas de arquivos de desenvolvimento com valores falsos; em produção, um cofre de segredos (Azure Key Vault, AWS Secrets Manager ou HashiCorp Vault) com rotação. |
| Logs e erros | Logs sem tokens, descrição de lançamento ou dados pessoais. Respostas de erro no formato Problem Details (RFC 7807), sem stack trace. |
| Cache | O Redis guarda apenas posições de dias fechados, indexadas por `contaId`, com expiração. |

### 4.3 Integridade e auditoria

| Controle | Como |
|---|---|
| Trilha de auditoria | Lançamentos são somente inclusão; correções são estornos. O histórico completo é a própria auditoria. |
| Quem fez o quê | Cada lançamento fica gravado com a conta (e, por ela, o titular), o instante de registro e a chave de idempotência. Os logs ligam a operação pelo `correlacaoId` e pelo trace, sem o titular. Em produção, um log de auditoria separado, com acesso restrito, registraria também o cliente autenticado. |
| Privilégio mínimo no banco | O usuário da aplicação não tem permissão de `UPDATE` nem `DELETE` na tabela de lançamentos. |
| Entrada de dados | Validação na borda (valores positivos, tamanho máximo de `descricao`, formato da `ChaveIdempotencia`) e consultas sempre parametrizadas (EF Core). |
| Dependências | O NuGet Audit verifica as vulnerabilidades conhecidas dos pacotes a cada restauração. Como todo warning é erro, um pacote vulnerável quebra o build, inclusive no CI. |

### 4.4 No desafio

Entram no código: validação JWT nos serviços, escopos, verificação de posse da conta, rate limiting, segredos por variáveis de ambiente, logs sem dados sensíveis, Problem Details, validação de entrada e auditoria de pacotes no build. Para rodar localmente, um emissor de tokens de desenvolvimento substitui o Provedor de Identidade. TLS na borda (o ambiente local usa HTTP por simplicidade), TLS interno, mTLS, cofre de segredos, criptografia em nível de coluna e privilégio mínimo no banco ficam documentados como evolução para produção.

---

## 5. Trade-offs, desafios e evolução

### 5.1 Simplicidade × flexibilidade

Cada escolha que dá flexibilidade cobra complexidade. A regra foi pagar esse custo só onde um requisito do enunciado o exige, e manter simples o resto.

| Escolha | O que ganha | O que custa | Por que vale aqui |
|---|---|---|---|
| Dois microsserviços, um por Bounded Context | Lançamentos e Posição escalam, sofrem deploy e falham de forma independente | Broker, dois bancos, consistência eventual e mais peças para operar e observar | O enunciado exige que Lançamentos continue operando se o consolidado cair. Um monólito modular seria mais simples, mas uma falha ou um pico da consulta afetaria a escrita. A divisão para nos Bounded Contexts: quebrar mais (um serviço por caso de uso) só somaria custo |
| Consistência eventual na posição | Isolamento de falhas e escrita rápida, sem esperar a projeção | A posição pode estar alguns segundos atrasada | O atraso é exposto em `atualizadoAte`, e o saldo que autoriza débitos fica em Lançamentos, com consistência forte ([DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas)) |
| MassTransit sobre RabbitMQ | Outbox, Inbox, retentativas e DLQ já testados | Uma dependência com suas convenções e, na v9, licença comercial | Escrever esses mecanismos à mão, numa parte crítica, seria mais código e mais risco |
| API Gateway (YARP) | Uma única entrada pública, com rate limiting e timeout | Mais um salto de rede e mais um componente | É o ponto natural para proteger a borda e retirar destinos com falha |
| Cache Redis opcional | Consultas mais rápidas nos picos | Mais um componente e a invalidação na reconstrução | O cache é opcional: fora do ar, a consulta vai ao banco, mais lenta mas correta |
| DDD tático, camadas e testes de arquitetura | Regras de dinheiro isoladas, testáveis e protegidas contra regressões de desenho | Mais arquivos e mais cerimônia que um CRUD | O risco do domínio está em saldo errado ou lançamento perdido; vale isolar essas regras |

O que ficou simples de propósito, com o caminho para evoluir:

- **Entrega do Outbox dentro da `Lancamentos.Api`**, sem um worker dedicado. Mover para um worker é só configuração, se o volume pedir.
- **Rate limiting por instância do gateway.** Com várias instâncias, o limite efetivo se multiplica; um limite distribuído (Redis) entra quando houver escala horizontal do gateway.
- **Emissor de tokens de desenvolvimento** no lugar do Provedor de Identidade, só para rodar localmente.
- **`docker compose`** como ambiente, sem orquestrador. Os health checks e as APIs sem estado já estão prontos para um orquestrador com autoscaling.

### 5.2 Desafios enfrentados

| Desafio | Como foi resolvido |
|---|---|
| **Eventos fora de ordem e repetidos.** O broker não garante ordem, e a posição depende dela. | `Sequencia` por conta: o evento com lacuna fica pendente e é aplicado quando o anterior chega; a chave `(conta, sequencia)` e o Inbox descartam repetições; um lock por conta serializa a projeção ([3.1](#31-consistência-e-concorrência)). |
| **Débitos simultâneos na mesma conta.** A concorrência otimista sozinha gera retentativas justamente no caso mais disputado. | Bloqueio da linha da `Conta` em cada operação, com a `Versao` como proteção extra. O cenário com 10 débitos simultâneos prova que só os que cabem no saldo são aceitos. |
| **Gravar o lançamento e publicar o evento (dual write).** | Transactional Outbox: lançamento e mensagem na mesma transação, entregue quando o broker estiver disponível. |
| **Detectar divergência entre os contextos sem acoplá-los.** A primeira versão só documentava uma conciliação periódica entre os bancos. | O evento passou a levar o saldo disponível após a movimentação, e a Posição compara na mesma sequência. A divergência é detectada no movimento que a causou, sem chamada síncrona nem acesso ao banco do outro contexto ([DOMINIO 6.8](DOMINIO.md#68-conciliação-com-lançamentos)). |
| **Manter todo acesso a dados em LINQ.** `SELECT ... FOR UPDATE` e o lock por conta não têm equivalente em LINQ, e uma soma sobre o Value Object `Valor` não era traduzida. | O `FOR UPDATE` entra por `TagWith` e um interceptor do EF Core; o lock por conta virou uma função do banco mapeada com `HasDbFunction`; a soma usa a coluna por trás do conversor. Cada tradução foi conferida no SQL gerado. |
| **Fazer as convenções valerem com o tempo.** Uma regra só documentada se perde. | Testes de arquitetura para cada convenção, e cada regra provada com um teste de mutação (o código que a viola precisa fazer o teste falhar). Assim apareceu que o NetArchTest não enxergava o acesso a um `DbSet`: a verificação passou a ler o IL compilado. |
| **Licença do MassTransit.** A v9 passou a ter licença comercial. | Fixado na v8 (Apache 2.0), com a versão principal verificada por teste; a decisão fica registrada para reavaliação em produção. |
| **Build verde na máquina, vermelho no CI.** Uma regra genérica do `.gitignore` (`[Ll]ogs/`) ignorava uma pasta de código dos testes. | Exceção no `.gitignore` e validação a partir de um clone limpo, como o CI faz. |
| **Posição 22 minutos atrás da escrita.** O teste de carga mostrou a entrega do Outbox perto de 2 eventos por segundo, sem nenhum erro para o cliente. | A contagem de erros `40001` no log confirmou a causa antes de qualquer ajuste. O isolamento `ReadCommitted` na entrega bastou, e a nova medição comprovou ([3.3](#resultados-do-teste-de-carga)). |

### 5.3 Evolução

Em ordem de prioridade. Os itens vêm das colunas **Documentar** das seções 3 e 4 e do [DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras).

**1. Antes de produção: proteger e enxergar**

- TLS na borda (ingress ou load balancer) e para PostgreSQL, RabbitMQ e Redis; cofre de segredos com rotação ([4.2](#42-proteção-dos-dados)).
- WAF à frente do gateway (regras OWASP Top 10) e proteção contra DDoS do provedor de nuvem, somados ao rate limiting que já existe.
- Métricas e alertas da integração: mensagens pendentes no Outbox, profundidade da fila, mensagens na `_error` e atraso da posição ([3.4](#34-observabilidade)).
- Health check passivo do YARP como circuit breaker ([3.2](#32-indisponibilidade-parcial)) e privilégio mínimo no banco ([4.3](#43-integridade-e-auditoria)).

**2. Com o crescimento: volume e operação**

- Particionamento da tabela de lançamentos por data e réplicas de leitura para a posição ([3.3](#33-alta-demanda)).
- Reenvio de eventos por Lançamentos, para reconstruir a posição mesmo depois de perder eventos ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)).
- Rate limiting distribuído entre as instâncias do gateway; entrega do Outbox num worker dedicado.
- mTLS entre serviços, criptografia em nível de coluna da `descricao` e log de auditoria separado ([4.1](#41-autenticação-e-autorização), [4.2](#42-proteção-dos-dados), [4.3](#43-integridade-e-auditoria)).
- Reavaliar o MassTransit: licença da v9 ou migração para uma alternativa.

**3. Expansão do negócio**

- Partidas dobradas, base para transferências entre contas.
- Múltiplas moedas, lançamentos agendados e um contexto de Clientes ([DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras)). A divisão em Bounded Contexts e o contrato de eventos versionado permitem acrescentá-los sem reescrever os atuais.

---

## Legenda dos diagramas C4

Os diagramas são escritos em Mermaid e exibidos diretamente pelo GitHub. Os diagramas C4 seguem a notação visual do C4 (cores, tipos de elemento e tecnologia entre colchetes), mas usam `flowchart` em vez da sintaxe `C4Context` do Mermaid, que ainda é experimental e gera layouts difíceis de ler.

| Cor | Elemento |
|---|---|
| Azul escuro | Pessoa |
| Azul | Sistema em escopo |
| Cinza | Sistema externo |
| Azul médio | Container (aplicação, banco de dados, fila) |
| Azul claro | Component |
| Borda tracejada | Fronteira (sistema, Bounded Context ou container) |
| BC | Bounded Context |

---

## Relação entre DDD e C4

| Conceito de DDD | Nível | Onde aparece |
|---|---|---|
| Subdomain (Core, Supporting, Generic) | Estratégico | [1.1](diagramas/01-ddd-estrategico/01-subdomains.md) e o Provedor de Identidade como sistema externo |
| Bounded Context | Estratégico | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) e fronteiras no [1.4](diagramas/01-ddd-estrategico/04-c4-container.md): um microserviço por contexto |
| Upstream / Downstream | Estratégico | Direção do fluxo Lançamentos → RabbitMQ → Posição Consolidada |
| Published Language | Estratégico | Eventos `ContaAberta v1` e `MovimentacaoRegistrada v1` trafegando pelo broker |
| Sem Shared Kernel | Estratégico | Bancos separados e Value Objects duplicados em cada contexto |
| Aggregate Root, Value Object, Repository | Tático | [2.1](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md), [2.3](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) e o componente **Modelo de Domínio** |
| Domain Event → Integration Event | Tático | Tradutor de Eventos e Outbox do MassTransit ([2.2](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md)) e o ciclo de vida ([2.5](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md)) |
