# Spec Delta

## Purpose

Garante que um cliente só opera as contas de que é titular e que o sistema nunca revela, pela resposta, se existe uma conta que pertence a outra pessoa.

## ADDED Requirements

### Requirement: Operação só na própria conta
Toda operação sobre uma conta existente SHALL ser executada somente quando o `TitularId` do cliente autenticado é o titular da conta. Caso contrário, nada é alterado e nenhum evento é publicado.

#### Scenario: Crédito na conta de outro titular
- **WHEN** o cliente `A` chama `POST /lancamentos/contas/{contaId}/creditos` para uma conta cujo titular é `B`
- **THEN** nenhum lançamento é criado
- **AND** o saldo da conta não muda
- **AND** nenhum evento é publicado

### Requirement: Conta alheia indistinguível de conta inexistente
Quando a conta não existe ou pertence a outro titular, o sistema SHALL responder `404` com o mesmo código de erro (`conta-nao-encontrada`) e o mesmo corpo nos dois casos.

#### Scenario: Conta de outro titular
- **WHEN** o cliente `A` opera uma conta existente cujo titular é `B`
- **THEN** a resposta é `404` com código `conta-nao-encontrada`

#### Scenario: Conta inexistente
- **WHEN** o cliente opera um `contaId` que não existe
- **THEN** a resposta é `404` com código `conta-nao-encontrada`, idêntica à da conta de outro titular

### Requirement: Titularidade verificada antes da idempotência
A titularidade SHALL ser verificada antes da chave de idempotência e antes de qualquer outra regra de negócio. Um cliente que repete a chave usada por outro titular não recebe o resultado da operação original.

#### Scenario: Chave reaproveitada por outro cliente
- **WHEN** o cliente `B` registra um débito na sua conta com a chave `k1`
- **AND** o cliente `A` envia uma requisição para a conta de `B` com a mesma chave `k1`
- **THEN** a resposta para `A` é `404` com código `conta-nao-encontrada`
- **AND** a resposta não contém dados do lançamento de `B`
