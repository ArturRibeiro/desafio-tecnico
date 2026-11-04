# Tasks

## 1. Diagnóstico

- [x] 1.1 Reproduzir o gargalo: escrita curta (`VUS=10 PATAMAR=20s`), contando no log de `lancamentos-api` os erros `40001` na reserva do `OutboxState` e a vazão de entrega (mensagens pendentes no Outbox ao longo do tempo). Verificação: números anotados e hipótese confirmada ou descartada (D1). Resultado: 87 erros `40001` numa escrita de 10 VUs por 20 s; cada um aciona a retentativa do MassTransit com espera crescente (7 s a 30 s), e o Outbox esvazia em saltos de uns 70 eventos; 369 s até zerar. Confirmada.

## 2. Correção

- [x] 2.1 Isolamento `ReadCommitted` na entrega do Outbox de Lançamentos, com o motivo no comentário (D2). Verificação: a mesma escrita curta sem `40001` no log e com o Outbox esvaziando no ritmo da escrita. Resultado: 0 erros `40001`; no máximo 8 mensagens pendentes durante a escrita; zerado 2 s depois do fim.
- [x] 2.2 Se a fila do Outbox ainda crescer durante a escrita, reduzir o intervalo de varredura e medir de novo (D3); senão, registrar que não foi preciso. Verificação: decisão com números na task. Resultado: não foi preciso; a fila não cresce com o isolamento corrigido (2.1).
- [x] 2.3 Build sem warnings e `dotnet test -c Release` passando, incluindo os cenários de `eventos-de-integracao`.

## 3. Medição depois

- [x] 3.1 Mover os resultados atuais para `carga/resultados/antes-ajuste-do-outbox/` (D4) e rodar `carga/executar.sh` com os padrões. Verificação: tabela e JSON novos em `carga/resultados/`, todas as etapas sem erro. Resultado: execução 1793787573 sem etapas com erro; alcance de 1319 s para 1,9 s, 725 s para 1,1 s e 1322 s para 21 s; 0 erros `40001`. A escrita do cenário de leitura teve p95 de 246 ms; duas repetições só desse cenário deram 76 e 86 ms.

## 4. Documentação

- [x] 4.1 ARQUITETURA 3.3: tabela nova, comparação antes × depois das linhas de escrita e alcance, gargalo descrito como resolvido (causa e correção); linha "Rajada de eventos" atualizada; 5.3 sem o item do ajuste do Outbox; 5.2 com o desafio. Verificação: links resolvem e números iguais aos de `carga/resultados/`.
- [x] 4.2 `openspec validate ajustar-entrega-do-outbox` sem erros.
