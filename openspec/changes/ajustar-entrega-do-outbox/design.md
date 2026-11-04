# Design

## Context

- Lançamentos usa o Transactional Outbox do MassTransit 8 no EF Core (`AddEntityFrameworkOutbox` com `UsePostgres` e `UseBusOutbox`), com as opções padrão. Por reflexão do pacote 8.5.12: `QueryDelay` 5 s, `QueryMessageLimit` 100, `MessageDeliveryLimit` 100, `QueryTimeout` 30 s; o isolamento padrão da entrega é `RepeatableRead`.
- Cada operação de escrita grava um `OutboxState` com uma mensagem. A entrega reserva um estado por vez: `SELECT *, xmin FROM "OutboxState" ORDER BY "Created" LIMIT 1 FOR UPDATE SKIP LOCKED`.
- Na medição de 03/11/2026, o log de `lancamentos-api` tem cerca de 1.500 erros `40001: could not serialize access due to concurrent update` nessa consulta em 3 horas de carga. Hipótese: em `RepeatableRead`, cada conflito aborta o ciclo, e a entrega espera o `QueryDelay` para tentar de novo. Isso explicaria a vazão de 2 eventos/s.
- Resultados atuais em `carga/resultados/` (execução 1793753536).

## Goals / Non-Goals

**Goals:**
- Entrega do Outbox no ritmo da escrita da carga calibrada, sem falhas de serialização no log.
- Comparação antes × depois gerada pelo mesmo script, com os mesmos parâmetros.

**Non-Goals:**
- Mudar garantias de entrega (pelo menos uma vez, ordem por conta pela `Sequencia`, tolerância ao broker fora).
- Ajustar o consumo da Posição.

## Decisions

**1. Confirmar a causa antes de corrigir.** Reproduzir com uma escrita curta e contar os `40001` no log; repetir com a correção. Se não houver conflito, a hipótese cai e a causa é investigada antes de qualquer ajuste.

**2. Isolamento `ReadCommitted` na entrega do Outbox.** A reserva já é exclusiva por `FOR UPDATE SKIP LOCKED`; o `RepeatableRead` não protege nada a mais e transforma concorrência normal em erro.
- Alternativa: retentativa automática do EF Core (`EnableRetryOnFailure`). Mascara o conflito, mantém o custo e vale para toda a aplicação, inclusive o `FOR UPDATE` das contas.

**3. Intervalo de varredura menor, só se a medição pedir.** Com o isolamento corrigido, a entrega imediata do bus outbox deve dar conta; se a fila do Outbox ainda crescer na escrita, reduzir o `QueryDelay` (1 s) e medir de novo. Cada ajuste fica justificado por número.

**4. "Antes" preservado.** Os resultados atuais vão para `carga/resultados/antes-ajuste-do-outbox/` (sem mudar o conteúdo); a nova execução grava em `carga/resultados/`. A ARQUITETURA 3.3 traz a tabela atual e uma comparação das linhas afetadas (escrita, alcances), com link para os dois.

## Risks / Trade-offs

- [Menos isolamento na entrega] → a exclusividade vem do lock da linha; a ordem por conta é garantida pela `Sequencia` na Posição, não pela entrega.
- [Variação entre execuções] → mesmo script, mesmos parâmetros e mesma máquina; a diferença esperada (ordem de grandeza) é bem maior que a variação observada entre as duas execuções anteriores.
- [Testes de integração do Outbox] → os cenários de `eventos-de-integracao` (broker fora, falha ao confirmar a transação) precisam continuar passando.

## Migration Plan

Só configuração; sem migração de banco. Para reverter, voltar as opções padrão.

## Open Questions

- Se o `ReadCommitted` sozinho basta ou se o `QueryDelay` também precisa mudar (decidido pela medição, D3).
