## Why

O saldo de cada conta é calculado em dois lugares: Lançamentos, que autoriza débitos, e Posição Consolidada, que o mostra ao cliente. Hoje há mecanismos para evitar que os dois divirjam, mas nenhum para detectar a divergência quando ela acontece, por exemplo por um defeito no cálculo da projeção. O enunciado do desafio afirma que qualquer inconsistência gera impacto direto ao cliente; uma divergência silenciosa é justamente esse impacto.

## What Changes

- O aviso de movimentação de Lançamentos passa a informar o saldo disponível da conta logo após a movimentação. O campo é novo e opcional, então a versão 1 do evento continua compatível.
- A Posição Consolidada guarda esse saldo em cada movimentação recebida e, ao aplicá-la na ordem da conta, compara o saldo que calculou com o informado por Lançamentos.
- Uma divergência é registrada para análise, identificando a conta e a sequência, sem bloquear a projeção nem alterar o saldo calculado.
- A reconstrução da posição faz a mesma comparação, o que permite confirmar a correção depois de consertar um defeito de cálculo.
- A comparação é feita na mesma sequência, nunca "no momento atual", para que o atraso da consistência eventual não gere falsos alarmes.
- Movimentações recebidas sem o saldo informado (eventos anteriores a esta change) são aplicadas sem comparação.

## Fora do escopo

- Correção automática da posição divergente: a divergência é apenas sinalizada, e a correção continua sendo manual (correção do defeito e reconstrução).
- Exportação de métricas e alertas para um sistema de monitoramento; continua como evolução em "Saúde da integração".
- Detecção de atraso da projeção (eventos parados na fila de falhas), que é responsabilidade da métrica de atraso, também documentada como evolução.
- Conciliação periódica entre os bancos dos dois contextos.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `eventos-de-integracao`: o aviso de movimentação passa a trazer o saldo disponível após a movimentação.
- `projecao-da-posicao`: nova regra de conciliação de cada movimentação com o saldo informado por Lançamentos, inclusive na reconstrução.

## Impact

- Contrato de integração (Published Language) e o tradutor de eventos de Lançamentos.
- Domínio e persistência da Posição Consolidada (novo dado por movimentação, com migração), tradutor de eventos e logs do Worker.
- Documentação: DOMINIO (contrato e projeção) e ARQUITETURA (a conciliação deixa de ser só documentada).
- Testes: contrato comparado ao DOMINIO, unidade da projeção e cenários de negócio.
