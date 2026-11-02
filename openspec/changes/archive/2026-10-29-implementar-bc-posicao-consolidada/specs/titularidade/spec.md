# Spec Delta

## ADDED Requirements

### Requirement: Consulta só da própria conta
Toda consulta de posição ou de fechamentos DEVE ser respondida somente quando o `TitularId` do cliente autenticado é o titular da conta. Caso contrário, a resposta é `404` com o código `conta-nao-encontrada`, igual à de uma conta inexistente.

#### Scenario: Consulta da conta de outro titular
- **GIVEN** a conta `C` do titular `B`
- **WHEN** o cliente `A` consulta a posição de `C`
- **THEN** a resposta é `404` com o código `conta-nao-encontrada`
- **AND** o corpo é idêntico ao da consulta de um `contaId` inexistente

### Requirement: Conta ainda não projetada
Uma conta cuja abertura ainda não foi processada pela Posição Consolidada DEVE ser tratada como inexistente nas consultas. Assim que a abertura é processada, o titular passa a consultá-la normalmente.

#### Scenario: Consulta logo após a abertura
- **GIVEN** que o cliente acabou de abrir a conta `C` e o evento de abertura ainda não foi processado
- **WHEN** ele consulta a posição de `C`
- **THEN** a resposta é `404` com o código `conta-nao-encontrada`
- **AND** depois que o evento é processado, a mesma consulta responde `200`
