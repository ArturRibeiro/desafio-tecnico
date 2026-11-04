# 2.5 Ciclo de vida de um débito

> **DDD Tático** · [Índice](../../ARQUITETURA.md) · Anterior: [2.4 BC Posição Consolidada — C4 Component](04-posicao-consolidada-c4-component.md)

O caminho de um débito pelos dois Bounded Contexts, do comando até a posição consolidada.

```mermaid
sequenceDiagram
    autonumber
    actor Cliente
    participant UC as Caso de Uso<br/>RegistrarDebito
    participant Conta as Conta<br/>(Aggregate Root)
    participant DB as lancamentos-db
    participant Pub as Entrega do Outbox<br/>(MassTransit)
    participant MQ as RabbitMQ
    participant FD as FechamentoDiario<br/>(Aggregate Root)

    Cliente->>UC: RegistrarDebito(conta, valor, chave) com token
    UC->>DB: Abre a transação e carrega a Conta com SELECT ... FOR UPDATE
    Note over UC,DB: Operações na mesma conta passam a correr em série até o commit
    UC->>Conta: PertenceA(TitularId do token)?
    alt Conta inexistente ou de outro titular
        UC-->>Cliente: 404 conta-nao-encontrada
    else Chave já usada nesta conta
        UC->>DB: Busca lançamento pela ChaveIdempotencia
        DB-->>UC: Lançamento existente
        UC-->>Cliente: Mesmo conteúdo: resposta original (nada é criado). Outro conteúdo: 409
    else Titular confere e chave nova
        UC->>Conta: RegistrarDebito(valor, ...)
        Conta->>Conta: Valida invariantes (situação, limite)
        Conta-->>UC: Lancamento + Domain Event LancamentoRegistrado
        UC->>DB: SaveChanges e commit: Conta (Versao), Lancamento e OutboxMessage juntos
        UC-->>Cliente: 201 Created
        Pub->>DB: Lê pendentes do Outbox
        Pub->>MQ: Publica MovimentacaoRegistrada v1 (Integration Event)
        MQ->>FD: Consumer MassTransit entrega (pelo menos uma vez, Inbox descarta duplicatas)
        FD->>FD: Sequencia já aplicada é ignorada, com lacuna fica pendente, senão atualiza o fechamento do dia
    end
```
