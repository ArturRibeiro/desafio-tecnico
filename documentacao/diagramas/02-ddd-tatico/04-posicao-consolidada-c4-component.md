# 2.4 BC Posição Consolidada — C4 Component

> **DDD Tático** · [Índice](../../ARQUITETURA.md) · Anterior: [2.3 BC Posição Consolidada — Modelo de domínio](03-posicao-consolidada-modelo-de-dominio.md) · Próximo: [2.5 Ciclo de vida de um débito](05-ciclo-de-vida-de-um-debito.md)

Cores e formas seguem a [legenda dos diagramas C4](../../ARQUITETURA.md#legenda-dos-diagramas-c4).

```mermaid
flowchart TB
    broker[["<b>Message Broker</b><br/>[Container: RabbitMQ]"]]
    gateway["<b>API Gateway</b><br/>[Container: ASP.NET Core + YARP]"]

    subgraph worker["Posicao.Worker"]
        consumer["<b>Consumidores de Eventos</b><br/>[Component: MassTransit Consumer + Inbox]<br/><br/>Recebem ContaAberta v1 e MovimentacaoRegistrada v1,<br/>descartam duplicatas e enviam falhas para a fila _error"]
        tradutor["<b>Tradutor de Eventos</b><br/>[Component: Infrastructure]<br/><br/>Converte a Published Language no modelo<br/>do contexto; evento inválido vai direto para a _error"]
        projetar["<b>Projeção</b><br/>[Component: Application]<br/><br/>Grava o Movimento e, com lock por conta,<br/>aplica os pendentes em ordem de Sequencia"]
        reconstruir["<b>Reconstrução</b><br/>[Component: Application]<br/><br/>Comando reconstruir: recalcula os fechamentos<br/>a partir dos movimentos e limpa o cache"]
        dominioPos["<b>Modelo de Domínio</b><br/>[Component: Domain]<br/><br/>Titularidade, FechamentoDiario, Movimento<br/>e o Domain Service ProjecaoDaConta"]
    end

    subgraph api["Posicao.Api"]
        autenticacao["<b>Autenticação JWT</b><br/>[Component: Plataforma.Hospedagem]<br/><br/>Valida o token e exige<br/>o escopo posicao:ler"]
        endpointsPos["<b>Endpoints de Posição</b><br/>[Component: Minimal API]<br/><br/>Extrai o TitularId do sub, valida a entrada<br/>e devolve erros em Problem Details"]
        consultas["<b>Consultas</b><br/>[Component: Application]<br/><br/>Conferem a Titularidade e executam<br/>ObterPosicaoEm e ObterFechamentosDiarios"]
    end

    posDb[("<b>posicao-db</b><br/>[Container: PostgreSQL]")]
    cache[("<b>Cache</b><br/>[Container: Redis]")]

    broker -- "Entrega eventos [AMQP]" --> consumer
    consumer -- "Traduz" --> tradutor
    consumer -- "Envia o comando (IMediator)" --> projetar
    projetar -- "Aplica regras de negócio" --> dominioPos
    reconstruir -- "Reaplica os movimentos" --> dominioPos
    projetar -- "Grava titularidade, movimento,<br/>fechamento e Inbox [SQL]" --> posDb
    reconstruir -- "Regrava os fechamentos [SQL]" --> posDb
    reconstruir -- "Limpa [RESP]" --> cache

    gateway -- "Consultas com Bearer token [HTTP]" --> autenticacao
    autenticacao -- "Titular e escopos" --> endpointsPos
    endpointsPos -- "Envia a consulta (IMediator)" --> consultas
    consultas -- "Saldos de abertura definitivos [RESP]" --> cache
    consultas -- "Titularidade, fechamentos<br/>e movimentos [SQL]" --> posDb

    classDef container fill:#438dd5,stroke:#2e6295,color:#fff
    classDef component fill:#85bbf0,stroke:#5d82a8,color:#000
    class broker,gateway,posDb,cache container
    class consumer,tradutor,projetar,reconstruir,dominioPos,autenticacao,endpointsPos,consultas component
    style worker fill:none,stroke:#444,stroke-dasharray:6 4
    style api fill:none,stroke:#444,stroke-dasharray:6 4
```

O MassTransit cuida da entrega: Inbox (`InboxState`) para não processar o mesmo `eventoId` duas vezes, novas tentativas e fila `_error` como DLQ. Ele **não garante ordem**, então a ordem é regra do domínio:

- A `Titularidade` guarda a última sequência aplicada.
- O `ProjecaoDaConta` aplica os pendentes enquanto a sequência for a próxima esperada e para quando falta uma.
- O `FechamentoDiario` ignora uma sequência já aplicada.
- O lock por conta (`pg_advisory_xact_lock`) faz os eventos da mesma conta correrem um de cada vez, mesmo com várias instâncias do Worker.

Consumidores, endpoints e o comando `reconstruir` só conhecem o `IMediator` e os pedidos; cada comando ou consulta tem um único handler na Application.

O cache é opcional: com o Redis fora do ar, as consultas leem tudo do banco e respondem o mesmo saldo.
