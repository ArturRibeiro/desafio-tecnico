## ADDED Requirements

### Requirement: Conciliação com o saldo de Lançamentos
Ao aplicar cada movimentação, inclusive na reconstrução, a Posição Consolidada DEVE comparar o saldo que calculou com o saldo disponível informado por Lançamentos para a mesma sequência. Uma divergência gera um registro de erro com o código `saldo-divergente`, a conta, a sequência e o lançamento, sem valores, e não bloqueia a projeção nem altera o saldo calculado. Uma movimentação sem saldo informado é aplicada sem comparação.

#### Scenario: Saldos iguais
- **GIVEN** a conta C com saldo de R$ 100,00 na Posição Consolidada
- **WHEN** chega um débito de R$ 30,00 em que Lançamentos informa saldo de R$ 70,00 após a movimentação
- **THEN** o saldo de C passa a R$ 70,00
- **AND** nenhuma divergência é registrada

#### Scenario: Saldo divergente
- **GIVEN** a conta C com saldo de R$ 100,00 na Posição Consolidada
- **WHEN** chega um débito de R$ 30,00 em que Lançamentos informa saldo de R$ 50,00 após a movimentação
- **THEN** o saldo de C passa a R$ 70,00
- **AND** a divergência é registrada para análise, identificando a conta C e o lançamento
- **AND** as movimentações seguintes de C continuam sendo aplicadas

#### Scenario: Movimentação sem saldo informado
- **GIVEN** a conta C com saldo de R$ 100,00 na Posição Consolidada
- **WHEN** chega um crédito de R$ 10,00 sem o saldo informado por Lançamentos
- **THEN** o saldo de C passa a R$ 110,00
- **AND** nenhuma divergência é registrada

#### Scenario: Divergência na reconstrução
- **GIVEN** a conta C com uma movimentação recebida com saldo divergente
- **WHEN** a posição é reconstruída
- **THEN** a divergência é registrada de novo, identificando a conta C e o lançamento
