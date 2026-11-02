# Spec Delta

## MODIFIED Requirements

### Requirement: Abrir conta
Um cliente autenticado DEVE poder abrir uma conta com `POST /lancamentos/contas`. A conta nasce `Ativa`, em `BRL`, com saldo disponível zero, limite zero e o cliente autenticado como titular. A resposta é `201` com o identificador da conta, a situação, a moeda, o saldo disponível e o limite, e com o cabeçalho `Location`.

#### Scenario: Abertura bem-sucedida
- **WHEN** um cliente abre uma conta
- **THEN** a conta é aberta ativa, em reais, com saldo de R$ 0,00 e sem limite
- **AND** o cliente é o titular da conta

#### Scenario: Limite informado pelo cliente
- **WHEN** um cliente abre uma conta pedindo um limite de R$ 1.000,00
- **THEN** a conta é aberta sem limite

### Requirement: Identificador de conta não sequencial
O identificador da conta DEVE ser gerado pelo sistema, aleatório e não sequencial, para não expor volume nem permitir adivinhar contas de outros clientes.

#### Scenario: Duas aberturas seguidas
- **WHEN** um cliente abre duas contas, uma depois da outra
- **THEN** o número de uma conta não permite deduzir o número da outra

### Requirement: Abertura idempotente
A abertura DEVE exigir o cabeçalho `Idempotency-Key`. Repetir a abertura com a mesma chave, pelo mesmo titular, devolve a mesma conta, com o mesmo status e o mesmo corpo da primeira resposta, sem criar outra conta. A chave é única por titular.

#### Scenario: Repetição da abertura
- **WHEN** um cliente repete o mesmo pedido de abertura de conta
- **THEN** ele recebe a mesma conta da primeira vez
- **AND** só uma conta é aberta

#### Scenario: Mesma chave por titulares diferentes
- **WHEN** os clientes A e B fazem pedidos de abertura com a mesma identificação de pedido
- **THEN** cada um recebe a sua própria conta

#### Scenario: Abertura sem chave
- **WHEN** um cliente pede a abertura de uma conta sem identificar o pedido
- **THEN** a abertura é recusada por falta da identificação do pedido
