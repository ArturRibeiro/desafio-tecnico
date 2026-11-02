# Arquitetura e Diagramas

Este documento é o índice dos diagramas da solução. Cada diagrama fica em um arquivo próprio na pasta [`diagramas/`](diagramas/), na ordem do DDD:

1. **DDD Estratégico**: o problema visto de fora. Subdomains, Bounded Contexts, como eles se relacionam (Context Map) e como viram sistemas e containers (C4 System Context e Container).
2. **DDD Tático**: o interior de cada Bounded Context. Aggregates, Entities, Value Objects, Domain Events e os componentes que os implementam (C4 Component).

Depois dos diagramas, este documento mostra como a arquitetura responde aos cenários do enunciado: [atributos de qualidade](#3-atributos-de-qualidade) (instabilidade, alta demanda e falhas parciais) e [segurança e proteção de dados](#4-segurança-e-proteção-de-dados).

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
| 2.1 | [BC Lançamentos — Modelo de domínio](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md) | Aggregate Root `Conta`, Entity `Lancamento`, Value Objects e Domain Events |
| 2.2 | [BC Lançamentos — C4 Component](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md) | Camadas da API, com o Outbox do MassTransit |
| 2.3 | [BC Posição Consolidada — Modelo de domínio](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) | Aggregate Root `FechamentoDiario`, Entity `Movimento` e Value Objects |
| 2.4 | [BC Posição Consolidada — C4 Component](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Consumo de eventos e consultas |
| 2.5 | [Ciclo de vida de um débito](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md) | Do comando até a posição consolidada, passando pelos dois Bounded Contexts |

---

## 3. Atributos de qualidade

Como cada cenário do enunciado é tratado, qual mecanismo responde por ele e onde ele aparece na documentação.

A coluna **No desafio** mostra a priorização: **Implementado** está no código entregue, com o teste automatizado que o comprova. Nos fluxos de negócio, a prova é um cenário da spec executado pelo `Scenarios.Integration.Specs.Tests` contra os serviços, o PostgreSQL, o RabbitMQ e o Redis reais. **Implementado, sem teste automatizado** está no código, mas é verificado no ambiente local (são comportamentos técnicos, das specs técnicas). **Documentar** fica descrito como seria feito em produção, sem implementação.

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
| Saldo de Lançamentos diferente da posição consolidada | Rotina de conciliação periódica compara `SaldoDisponivel` com a posição e alerta divergências. | — | Documentar |

### 3.2 Indisponibilidade parcial

| Cenário | Comportamento | Onde | No desafio |
|---|---|---|---|
| RabbitMQ fora | Lançamentos continua aceitando operações; as mensagens acumulam no Outbox e são entregues quando o broker volta. A posição fica atrasada, e o atraso aparece em `atualizadoAte`. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado: cenário [`Broker fora durante as operações`](tests/Scenarios.Integration.Specs.Tests/Features/EventosDeIntegracao.feature) |
| Posição Consolidada fora | Lançamentos não é afetado (não há chamada síncrona entre os contextos). Ao voltar, o Worker consome o que ficou na fila. | [1.2](diagramas/01-ddd-estrategico/02-context-map.md) | Implementado: cenário [`Worker volta depois de parado`](tests/Scenarios.Integration.Specs.Tests/Features/ProjecaoDaPosicao.feature) |
| Redis fora | O cache é opcional: a consulta vai direto ao banco, mais lenta mas correta. | [2.4](diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md) | Implementado: cenário [`Cache fora do ar`](tests/Scenarios.Integration.Specs.Tests/Features/ConsultaDePosicao.feature) |
| Banco de Lançamentos fora | A operação falha sem gravar nada (`500`, sem detalhes internos), e a prontidão responde `503`, tirando a instância do balanceamento. Em dinheiro, é melhor recusar do que perder. | — | Implementado: cenário [`Falha inesperada`](tests/Scenarios.Integration.Specs.Tests/Features/Lancamentos.feature); a resposta `503` da prontidão não tem teste automatizado |
| Instância com problema | Health checks (`/health/live`, `/health/ready`) para o orquestrador retirar a instância do balanceamento. | — | Implementado, sem teste automatizado |
| Serviço interno lento | Timeouts e circuit breaker no API Gateway (`Microsoft.Extensions.Http.Resilience`), para a lentidão de um serviço não esgotar os recursos do outro. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Documentar |

### 3.3 Alta demanda

| Cenário | Mecanismo | Onde | No desafio |
|---|---|---|---|
| Pico de escritas | APIs sem estado, escaláveis horizontalmente. A `Conta` não carrega o histórico, então cada operação lê e grava pouco; a disputa existe só entre operações da mesma conta. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md), [DOMINIO 8](DOMINIO.md#8-decisões-de-modelagem-e-alternativas) | Implementado no desenho; sem teste de carga |
| Pico de consultas | `Posicao.Api` escala separada da escrita. A posição é o fechamento do dia anterior somado aos movimentos do dia, sem varrer o histórico; saldos de abertura definitivos vêm do cache. | [DOMINIO 6.3](DOMINIO.md#63-cálculo-da-posição-em-um-instante) | Implementado no desenho; sem teste de carga |
| Rajada de eventos | A fila amortece o pico; o `Posicao.Worker` escala pelo número de instâncias e pela concorrência do consumer (`Consumo__LimiteDeMensagensConcorrentes`). | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado no desenho; sem teste de carga |
| Cliente abusivo ou sobrecarga | Rate limiting por cliente no API Gateway (janela fixa por titular de token válido, ou por IP), respondendo `429` com `Retry-After`. O limite vale por instância do gateway. | [1.4](diagramas/01-ddd-estrategico/04-c4-container.md) | Implementado, sem teste automatizado |
| Crescimento do volume de dados | Particionamento da tabela de lançamentos por data e réplicas de leitura para a posição consolidada. | — | Documentar |

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
| Em trânsito | HTTPS (TLS 1.2+) do cliente ao gateway; em produção, TLS também para PostgreSQL, RabbitMQ e Redis. |
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

Entram no código: validação JWT nos serviços, escopos, verificação de posse da conta, rate limiting, segredos por variáveis de ambiente, logs sem dados sensíveis, Problem Details, validação de entrada e auditoria de pacotes no build. Para rodar localmente, um emissor de tokens de desenvolvimento substitui o Provedor de Identidade. TLS interno, mTLS, cofre de segredos, criptografia em nível de coluna e privilégio mínimo no banco ficam documentados como evolução para produção.

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
| Aggregate Root, Entity, Value Object | Tático | [2.1](diagramas/02-ddd-tatico/01-lancamentos-modelo-de-dominio.md), [2.3](diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md) e o componente **Modelo de Domínio** |
| Domain Event → Integration Event | Tático | Tradutor de Eventos e Outbox do MassTransit ([2.2](diagramas/02-ddd-tatico/02-lancamentos-c4-component.md)) e o ciclo de vida ([2.5](diagramas/02-ddd-tatico/05-ciclo-de-vida-de-um-debito.md)) |
