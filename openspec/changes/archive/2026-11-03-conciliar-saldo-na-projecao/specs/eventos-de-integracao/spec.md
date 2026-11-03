## MODIFIED Requirements

### Requirement: Evento de movimentação registrada
Todo crédito, débito ou estorno aceito DEVE publicar um único `MovimentacaoRegistrada` versão `1`, com:
- `eventoId`, `tipoEvento`, `versao`, `contaId`, `lancamentoId`;
- `sequencia`, `tipo`, `valor`, `moeda`;
- `dataCompetencia`, `dataRegistro`, `lancamentoEstornadoId` e `correlacaoId`;
- `saldoDisponivelApos`, o saldo disponível da conta logo após a movimentação. O campo é opcional no contrato, para manter a versão `1` compatível com avisos publicados antes dele.

#### Scenario: Débito aceito
- **GIVEN** uma conta com dois lançamentos e saldo de R$ 100,00
- **WHEN** o titular debita R$ 30,00
- **THEN** Lançamentos avisa a movimentação: débito de R$ 30,00, terceiro lançamento da conta, sem lançamento estornado e com saldo de R$ 70,00 após a movimentação

#### Scenario: Estorno aceito
- **GIVEN** um débito registrado
- **WHEN** o titular estorna esse débito
- **THEN** Lançamentos avisa a movimentação como estorno, indicando o débito estornado

#### Scenario: Operação recusada
- **WHEN** um débito é recusado por saldo insuficiente
- **THEN** Lançamentos não avisa nenhuma movimentação
