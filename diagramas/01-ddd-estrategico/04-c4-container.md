# 1.4 C4 — Container

> **DDD Estratégico** · [Índice](../../ARQUITETURA.md) · Anterior: [1.3 C4 — System Context](03-c4-system-context.md) · Próximo: [2.1 BC Lançamentos — Modelo de domínio](../02-ddd-tatico/01-lancamentos-modelo-de-dominio.md)

Cores e formas seguem a [legenda dos diagramas C4](../../ARQUITETURA.md#legenda-dos-diagramas-c4).

Cada Bounded Context é um microserviço independente, com seu próprio banco de dados: **Lançamentos** (Core, Upstream) e **Posição Consolidada** (Supporting, Downstream). A única ligação entre eles são os eventos `ContaAberta v1` e `MovimentacaoRegistrada v1`, publicados no RabbitMQ (**Published Language**).

```mermaid
flowchart TB
    cliente["<b>Cliente</b><br/>[Pessoa]"]
    idp["<b>Provedor de Identidade</b><br/>[Sistema externo]"]

    subgraph sistema["Sistema de Movimentações Financeiras"]
        gateway["<b>API Gateway</b><br/>[Container: ASP.NET Core + YARP]<br/><br/>Rate limiting por cliente (titular<br/>do token ou origem), correlação<br/>e roteamento"]

        subgraph bcLanc["BC Lançamentos"]
            lancApi["<b>Lancamentos.Api</b><br/>[Container: ASP.NET Core + MassTransit]<br/><br/>Registra créditos, débitos e estornos<br/>e entrega o Outbox ao broker"]
            lancDb[("<b>lancamentos-db</b><br/>[Container: PostgreSQL]<br/><br/>Contas, lançamentos<br/>(somente inclusão) e Outbox")]
        end

        broker[["<b>Message Broker</b><br/>[Container: RabbitMQ]<br/><br/>ContaAberta v1 e<br/>MovimentacaoRegistrada v1<br/>com DLQ"]]

        subgraph bcPos["BC Posição Consolidada"]
            posWorker["<b>Posicao.Worker</b><br/>[Container: .NET Worker Service + MassTransit]<br/><br/>Consome eventos de forma idempotente<br/>e atualiza fechamentos e movimentos"]
            posDb[("<b>posicao-db</b><br/>[Container: PostgreSQL]<br/><br/>FechamentoDiario, Movimento,<br/>Titularidade e Inbox")]
            posApi["<b>Posicao.Api</b><br/>[Container: ASP.NET Core]<br/><br/>Informa a posição em um instante<br/>e os fechamentos diários"]
            cache[("<b>Cache</b><br/>[Container: Redis]<br/><br/>Posições de dias já fechados")]
        end
    end

    cliente -- "Registra movimentações e<br/>consulta posição [HTTPS/JSON]" --> gateway
    gateway -- "Valida tokens para identificar<br/>o cliente [HTTPS/JWKS]" --> idp
    gateway -- "Encaminha comandos<br/>[HTTP]" --> lancApi
    gateway -- "Encaminha consultas<br/>[HTTP]" --> posApi

    lancApi -- "Grava conta, lançamento e Outbox<br/>na mesma transação [SQL]" --> lancDb
    lancApi -- "Entrega do Outbox: publica os<br/>Integration Events [AMQP]" --> broker

    broker -- "Entrega eventos (pelo menos uma vez)<br/>[AMQP]" --> posWorker
    posWorker -- "Atualiza Titularidade,<br/>FechamentoDiario e Movimento [SQL]" --> posDb
    posApi -- "Consulta posição<br/>[SQL]" --> posDb
    posApi -- "Lê e grava posições fechadas<br/>[RESP]" --> cache

    classDef pessoa fill:#08427b,stroke:#052e56,color:#fff
    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    classDef container fill:#438dd5,stroke:#2e6295,color:#fff
    class cliente pessoa
    class idp externo
    class gateway,lancApi,lancDb,broker,posWorker,posDb,posApi,cache container
    style sistema fill:none,stroke:#444,stroke-dasharray:6 4
    style bcLanc fill:none,stroke:#1168bd,stroke-width:2px,stroke-dasharray:6 4
    style bcPos fill:none,stroke:#1168bd,stroke-width:2px,stroke-dasharray:6 4
```

| Container | Bounded Context | Por que existe separado |
|---|---|---|
| **API Gateway** | — | Ponto único de entrada: rate limiting por cliente e correlação num só lugar. Ele valida o token só para identificar o cliente; a autorização fica em cada serviço, que valida o token de novo e confere os escopos (as setas dos serviços ao Provedor de Identidade foram omitidas). |
| **Lancamentos.Api** | Lançamentos | Caminho crítico de escrita, com consistência forte. A entrega do Outbox roda dentro dela (Delivery Service do MassTransit): a requisição só grava no banco, então se o broker estiver fora a API continua aceitando lançamentos e as mensagens aguardam no Outbox. |
| **Posicao.Worker** | Posição Consolidada | Hospeda apenas o consumer do MassTransit. Fica separado da API para que consumo e consultas escalem e falhem de forma independente. |
| **Posicao.Api** | Posição Consolidada | Leitura com volume alto nos picos, escalável horizontalmente. |
| **RabbitMQ** | — | Desacopla os contextos: uma falha em Posição Consolidada não afeta o registro de lançamentos. |

Todos os containers de aplicação exportam traces OpenTelemetry (OTLP) para um coletor, que no ambiente local é o Jaeger. Ele fica fora do diagrama por não fazer parte do fluxo de negócio.

### Por que não há um `Lancamentos.Worker`

O Transactional Outbox do MassTransit grava a mensagem na tabela de Outbox dentro da mesma transação do `SaveChanges` e a entrega ao RabbitMQ por um hosted service, com lock no banco para que várias instâncias da API não publiquem a mesma mensagem. Um processo separado só para publicar não traria isolamento adicional. Se a entrega um dia pesar na API, ela pode ser desligada ali e movida para um Worker próprio, mudando apenas a configuração. A decisão está registrada em [DOMINIO.md](../../DOMINIO.md#8-decisões-de-modelagem-e-alternativas).
