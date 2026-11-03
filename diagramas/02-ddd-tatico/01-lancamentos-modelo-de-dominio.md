# 2.1 BC Lançamentos — Modelo de domínio

> **DDD Tático** · [Índice](../../ARQUITETURA.md) · Anterior: [1.4 C4 — Container](../01-ddd-estrategico/04-c4-container.md) · Próximo: [2.2 BC Lançamentos — C4 Component](02-lancamentos-c4-component.md)

A `Conta` é o Aggregate Root e a única porta de entrada para criar lançamentos. Ela **produz** um `Lancamento` a cada operação, mas não guarda a coleção de lançamentos: o `Lancamento` é um Aggregate Root próprio, gravado na mesma transação (ver [DOMINIO.md](../../DOMINIO.md), seções 4.3 e 5.1).

```mermaid
classDiagram
    direction LR

    class Conta {
        <<Aggregate Root>>
        +ContaId Id
        +TitularId TitularId
        +ChaveIdempotencia ChaveAbertura
        +decimal SaldoDisponivel
        +decimal Limite
        +SituacaoConta Situacao
        +long UltimaSequencia
        +int Versao
        +Abrir(id, titular, chaveAbertura, agora)$ Conta
        +PertenceA(titularId) bool
        +RegistrarCredito(valor, descricao, dataCompetencia, chave, agora) Resultado~Lancamento~
        +RegistrarDebito(valor, descricao, dataCompetencia, chave, agora) Resultado~Lancamento~
        +Estornar(original, jaEstornado, descricao, chave, agora) Resultado~Lancamento~
    }

    class Lancamento {
        <<Aggregate Root>>
        +LancamentoId Id
        +ContaId ContaId
        +TipoLancamento Tipo
        +Valor Valor
        +Descricao Descricao
        +DateTimeOffset DataCompetencia
        +DateTimeOffset DataRegistro
        +long Sequencia
        +LancamentoId? LancamentoEstornadoId
        +ChaveIdempotencia ChaveIdempotencia
        +decimal SaldoDisponivelApos
    }

    class Valor {
        <<Value Object>>
        +decimal Quantia
        +string Moeda
    }
    class ContaId {
        <<Value Object>>
    }
    class LancamentoId {
        <<Value Object>>
    }
    class TitularId {
        <<Value Object>>
    }
    class Descricao {
        <<Value Object>>
    }
    class ChaveIdempotencia {
        <<Value Object>>
    }
    class TipoLancamento {
        <<enumeration>>
        Credito
        Debito
        Estorno
    }
    class SituacaoConta {
        <<enumeration>>
        Ativa
        Bloqueada
        Encerrada
    }

    class ContaAberta {
        <<Domain Event>>
    }
    class LancamentoRegistrado {
        <<Domain Event>>
    }
    class LancamentoEstornado {
        <<Domain Event>>
    }

    Conta ..> Lancamento : produz
    Conta ..> ContaAberta : dispara
    Conta ..> LancamentoRegistrado : dispara
    Conta ..> LancamentoEstornado : dispara
    Conta *-- ContaId
    Conta *-- TitularId
    Conta *-- Valor
    Conta *-- SituacaoConta
    Lancamento *-- LancamentoId
    Lancamento *-- TipoLancamento
    Lancamento *-- Descricao
    Lancamento *-- ChaveIdempotencia
    Lancamento --> ContaId : referencia por Id
```

| Elemento tático | Papel |
|---|---|
| **Aggregate Root** `Conta` | Protege as invariantes: situação ativa, limite de saldo, sequência sem lacunas e titular fixo. `PertenceA` é a verificação de titularidade. |
| **Aggregate Root** `Lancamento` | Tem identidade própria, Repository próprio e é imutável depois de criado. Só a `Conta` o cria, na mesma transação; referencia a conta só pelo `ContaId`. |
| **Value Objects** | `Valor`, `ContaId`, `TitularId`, `LancamentoId`, `Descricao`, `ChaveIdempotencia`: sem identidade, imutáveis e autovalidados. |
| **Domain Events** | `ContaAberta`, `LancamentoRegistrado` e `LancamentoEstornado`: disparados pela `Conta` e convertidos em Integration Event no Outbox. |
