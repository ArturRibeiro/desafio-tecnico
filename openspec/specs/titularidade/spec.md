# titularidade Specification

## Purpose

Garante que um cliente só opera as contas de que é titular e que o sistema nunca revela, pela resposta, se existe uma conta que pertence a outra pessoa.

## Requirements

### Requirement: Operação só na própria conta
Toda operação sobre uma conta existente DEVE ser executada somente quando o `TitularId` do cliente autenticado é o titular da conta. Caso contrário, nada é alterado e nenhum evento é publicado.

#### Scenario: Crédito na conta de outro titular
- **WHEN** o cliente A tenta creditar R$ 50,00 na conta do cliente B
- **THEN** nenhum lançamento é registrado
- **AND** o saldo da conta de B não muda
- **AND** a Posição Consolidada não é avisada de nenhuma movimentação

### Requirement: Conta alheia indistinguível de conta inexistente
Quando a conta não existe ou pertence a outro titular, o sistema DEVE responder `404` com o mesmo código de erro (`conta-nao-encontrada`) e o mesmo corpo nos dois casos.

#### Scenario: Conta de outro titular
- **WHEN** o cliente A tenta operar a conta do cliente B
- **THEN** a operação é recusada porque a conta não foi encontrada

#### Scenario: Conta inexistente
- **WHEN** o cliente tenta operar uma conta que não existe
- **THEN** a operação é recusada porque a conta não foi encontrada, exatamente como na conta de outro titular

### Requirement: Titularidade verificada antes da idempotência
A titularidade DEVE ser verificada antes da chave de idempotência e antes de qualquer outra regra de negócio. Um cliente que repete a chave usada por outro titular não recebe o resultado da operação original.

#### Scenario: Chave reaproveitada por outro cliente
- **GIVEN** que o cliente B debitou R$ 30,00 da sua conta
- **WHEN** o cliente A repete o mesmo pedido na conta de B
- **THEN** o pedido de A é recusado porque a conta não foi encontrada
- **AND** A não recebe nenhum dado do débito de B

### Requirement: Consulta só da própria conta
Toda consulta de posição ou de fechamentos DEVE ser respondida somente quando o `TitularId` do cliente autenticado é o titular da conta. Caso contrário, a resposta é `404` com o código `conta-nao-encontrada`, igual à de uma conta inexistente.

#### Scenario: Consulta da conta de outro titular
- **GIVEN** a conta C do cliente B
- **WHEN** o cliente A consulta a posição de C
- **THEN** a consulta é recusada porque a conta não foi encontrada
- **AND** a recusa é idêntica à da consulta de uma conta que não existe

### Requirement: Conta ainda não projetada
Uma conta cuja abertura ainda não foi processada pela Posição Consolidada DEVE ser tratada como inexistente nas consultas. Assim que a abertura é processada, o titular passa a consultá-la normalmente.

#### Scenario: Consulta logo após a abertura
- **GIVEN** que o cliente acabou de abrir a conta C e a Posição Consolidada ainda não tomou conhecimento da abertura
- **WHEN** ele consulta a posição de C
- **THEN** a consulta é recusada porque a conta não foi encontrada
- **AND** depois que a Posição Consolidada toma conhecimento da abertura, a mesma consulta mostra o saldo da conta
