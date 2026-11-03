# 2.2 BC Lançamentos — C4 Component

> **DDD Tático** · [Índice](../../ARQUITETURA.md) · Anterior: [2.1 BC Lançamentos — Modelo de domínio](01-lancamentos-modelo-de-dominio.md) · Próximo: [2.3 BC Posição Consolidada — Modelo de domínio](03-posicao-consolidada-modelo-de-dominio.md)

Cores e formas seguem a [legenda dos diagramas C4](../../ARQUITETURA.md#legenda-dos-diagramas-c4).

As dependências apontam sempre para o Modelo de Domínio, que não depende de nenhuma outra camada.

```mermaid
flowchart TB
    gateway["<b>API Gateway</b><br/>[Container: ASP.NET Core + YARP]"]

    subgraph api["Lancamentos.Api"]
        autenticacao["<b>Autenticação JWT</b><br/>[Component: Plataforma.Hospedagem]<br/><br/>Valida assinatura, emissor, audiência e validade;<br/>exige o escopo lancamentos:escrever"]
        endpoints["<b>Endpoints de Lançamentos</b><br/>[Component: Minimal API]<br/><br/>Extrai o TitularId do sub, valida a entrada<br/>e devolve erros em Problem Details"]
        handlers["<b>Casos de Uso</b><br/>[Component: Application + Mediator]<br/><br/>Um handler por comando: bloqueia a conta, confere<br/>a titularidade e a idempotência e executa o comando"]
        dominio["<b>Modelo de Domínio</b><br/>[Component: Domain]<br/><br/>Aggregate Roots Conta e Lancamento,<br/>Value Objects e Domain Events"]
        repos["<b>Repositórios</b><br/>[Component: Infrastructure / EF Core]<br/><br/>Carrega a Conta com SELECT ... FOR UPDATE;<br/>índices únicos como última barreira"]
        tradutor["<b>Tradutor de Eventos</b><br/>[Component: Infrastructure]<br/><br/>Converte Domain Events em ContaAberta v1<br/>e MovimentacaoRegistrada v1"]
        busOutbox["<b>Outbox Transacional</b><br/>[Component: MassTransit EF Core Outbox]<br/><br/>Grava a mensagem na mesma<br/>transação do SaveChanges"]
        delivery["<b>Entrega do Outbox</b><br/>[Component: MassTransit Delivery Service]<br/><br/>Publica as pendentes com lock<br/>entre instâncias e retentativas"]
    end

    lancDb[("<b>lancamentos-db</b><br/>[Container: PostgreSQL]")]
    broker[["<b>Message Broker</b><br/>[Container: RabbitMQ]"]]

    gateway -- "Comandos com Bearer token [HTTP]" --> autenticacao
    autenticacao -- "Titular e escopos" --> endpoints
    endpoints -- "Envia o comando (IMediator)" --> handlers
    handlers -- "Aplica regras de negócio" --> dominio
    handlers -- "Carrega e salva" --> repos
    repos -- "Domain Events ao confirmar" --> tradutor
    tradutor -- "Publish (IPublishEndpoint)" --> busOutbox
    repos -- "Lê e grava [SQL]" --> lancDb
    busOutbox -- "Grava OutboxMessage [SQL]" --> lancDb
    delivery -- "Lê pendentes [SQL]" --> lancDb
    delivery -- "Publica [AMQP]" --> broker

    classDef container fill:#438dd5,stroke:#2e6295,color:#fff
    classDef component fill:#85bbf0,stroke:#5d82a8,color:#000
    class gateway,lancDb,broker container
    class autenticacao,endpoints,handlers,dominio,repos,tradutor,busOutbox,delivery component
    style api fill:none,stroke:#444,stroke-dasharray:6 4
```

Os componentes **Outbox Transacional** e **Entrega do Outbox** são do MassTransit, apenas configurados (`AddEntityFrameworkOutbox` com `UseBusOutbox`). O código próprio do contexto é o **Tradutor de Eventos**, na Infraestrutura, que mantém o modelo interno separado da Published Language: Domínio e Aplicação não conhecem os contratos, e os testes de arquitetura garantem isso. Os endpoints só conhecem o `IMediator` e os comandos: o Mediator, da biblioteca técnica `Plataforma.Mediator` (compartilhada pelos dois contextos e sem pacote externo), entrega cada comando ao único handler registrado para ele, e um teste de arquitetura impede a borda de usar um handler diretamente. A **Autenticação JWT** vem da biblioteca técnica `Plataforma.Hospedagem`, reaproveitável pelos outros serviços.
