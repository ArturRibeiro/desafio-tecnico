# Spec Delta

## Purpose

Abertura de contas no contexto de Lançamentos. A conta é onde os lançamentos acontecem e guarda o titular, a situação, o limite e o saldo disponível usados para autorizar débitos.

## ADDED Requirements

### Requirement: Abrir conta
Um cliente autenticado SHALL poder abrir uma conta com `POST /lancamentos/contas`. A conta nasce `Ativa`, em `BRL`, com saldo disponível zero, limite zero e o cliente autenticado como titular. A resposta é `201` com o identificador da conta, a situação, a moeda, o saldo disponível e o limite, e com o cabeçalho `Location`.

#### Scenario: Abertura bem-sucedida
- **WHEN** um cliente autenticado chama `POST /lancamentos/contas` com uma `Idempotency-Key` nova
- **THEN** a resposta é `201` com `situacao = "Ativa"`, `moeda = "BRL"`, `saldoDisponivel = 0.00` e `limite = 0.00`
- **AND** o titular da conta é o `sub` do token

#### Scenario: Limite informado pelo cliente
- **WHEN** o corpo da abertura contém um campo `limite`
- **THEN** o campo é ignorado e a conta nasce com limite zero

### Requirement: Identificador de conta não sequencial
O identificador da conta SHALL ser gerado pelo sistema, aleatório e não sequencial, para não expor volume nem permitir adivinhar contas de outros clientes.

#### Scenario: Duas aberturas seguidas
- **WHEN** duas contas são abertas em sequência
- **THEN** seus identificadores não são consecutivos nem derivados um do outro

### Requirement: Abertura idempotente
A abertura SHALL exigir o cabeçalho `Idempotency-Key`. Repetir a abertura com a mesma chave, pelo mesmo titular, devolve a mesma conta, com o mesmo status e o mesmo corpo da primeira resposta, sem criar outra conta. A chave é única por titular.

#### Scenario: Repetição da abertura
- **WHEN** o cliente repete `POST /lancamentos/contas` com a mesma `Idempotency-Key`
- **THEN** a resposta é a mesma da primeira vez, com o mesmo `contaId`
- **AND** existe uma única conta para aquela chave

#### Scenario: Mesma chave por titulares diferentes
- **WHEN** dois titulares diferentes abrem contas com a mesma `Idempotency-Key`
- **THEN** cada um recebe a sua própria conta

#### Scenario: Abertura sem chave
- **WHEN** a abertura é chamada sem `Idempotency-Key`
- **THEN** a resposta é `400` com código `chave-idempotencia-obrigatoria`
