# Proposal

## Why

O teste de carga mediu o gargalo da solução: a entrega do Outbox de Lançamentos ao broker fica perto de 2 eventos por segundo, contra mais de 50 operações por segundo na escrita. Cerca de 2.800 operações levaram uns 22 minutos para chegar à posição consolidada. O log de Lançamentos mostra a causa provável: falhas de serialização na consulta com que a entrega reserva as mensagens, cada uma seguida de uma pausa até a próxima varredura.

## What Changes

- Confirmar a causa no ambiente local antes de corrigir.
- Ajustar a entrega do Outbox de Lançamentos para acompanhar o ritmo da escrita, com o menor ajuste que a medição comprovar.
- Preservar o resultado atual do teste de carga como "antes" e registrar a nova execução como "depois".
- Arquitetura: comparação antes × depois nos cenários de alta demanda e o gargalo marcado como resolvido; o item sai do roadmap de evolução.

## Capabilities

### New Capabilities
<!-- Nenhuma. -->

### Modified Capabilities
- `teste-de-carga`: a correção de um gargalo encontrado passa a ser registrada com a medição antes e depois.

## Impact

- Lançamentos: configuração da entrega do Outbox (infraestrutura). Contrato dos eventos, garantias de entrega e regras de negócio não mudam.
- Resultados do teste de carga e documentação de arquitetura.

## Fora do escopo

- Entrega do Outbox num worker dedicado e escala horizontal da entrega.
- Mudanças no consumo da Posição.
- Metas de SLA ou capacidade de produção.
