# Spec Delta

## ADDED Requirements

### Requirement: Limite de requisições por cliente
O gateway DEVE limitar a quantidade de requisições que cada cliente faz numa janela de tempo configurável. As requisições de um cliente não consomem o limite de outro.

#### Scenario: Cliente dentro do limite
- **GIVEN** um limite de 5 requisições por janela
- **WHEN** um cliente faz 5 requisições na mesma janela
- **THEN** todas são encaminhadas ao serviço de destino

#### Scenario: Um cliente não afeta outro
- **GIVEN** que o cliente `A` já esgotou o seu limite na janela
- **WHEN** o cliente `B` faz uma requisição
- **THEN** a requisição de `B` é encaminhada normalmente

#### Scenario: Nova janela
- **GIVEN** que o cliente esgotou o limite
- **WHEN** a janela termina e ele faz uma nova requisição
- **THEN** a requisição é encaminhada normalmente

### Requirement: Resposta ao exceder o limite
Uma requisição acima do limite DEVE receber `429` com o cabeçalho `Retry-After` e um Problem Details com o código `limite-excedido`, sem ser encaminhada ao serviço de destino.

#### Scenario: Requisição acima do limite
- **GIVEN** que o cliente já fez todas as requisições permitidas na janela
- **WHEN** ele faz mais uma
- **THEN** a resposta é `429` com o código `limite-excedido` e o cabeçalho `Retry-After` em segundos
- **AND** o serviço de destino não recebe a requisição

### Requirement: Cliente identificado pelo token ou pela origem
O cliente DEVE ser identificado pelo titular (`sub`) de um token com assinatura, emissor, audiência e validade válidos. Sem token, ou com token inválido, ele é identificado pelo endereço de origem da conexão. O gateway não recusa a requisição por causa do token; essa decisão continua nos serviços.

#### Scenario: Mesmo titular, origens diferentes
- **GIVEN** que o titular `T` esgotou o limite a partir de uma origem
- **WHEN** `T` faz uma requisição de outra origem, com um token válido
- **THEN** a resposta é `429`

#### Scenario: Token forjado não troca de identidade
- **GIVEN** que uma origem esgotou o limite sem token válido
- **WHEN** ela envia uma requisição com um token de assinatura inválida e outro `sub`
- **THEN** a resposta é `429`, porque o cliente continua identificado pela origem

#### Scenario: Token inválido segue para o serviço
- **GIVEN** um cliente dentro do limite
- **WHEN** ele envia um token inválido
- **THEN** a requisição é encaminhada, e o serviço de destino decide a resposta (`401`)

### Requirement: Saúde fora do limite
As requisições a `/health/live` e `/health/ready` do gateway NÃO DEVEM consumir nem ser barradas pelo limite.

#### Scenario: Sondas com o limite esgotado
- **GIVEN** que a origem do orquestrador esgotou o limite
- **WHEN** ele chama `/health/ready` no gateway
- **THEN** a resposta não é `429`
