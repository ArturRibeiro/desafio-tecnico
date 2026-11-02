# api-gateway Specification

## Purpose

O API Gateway é o único ponto de entrada externo do sistema. Ele encaminha cada requisição ao serviço do Bounded Context correspondente, para que os serviços internos nunca fiquem expostos diretamente.

## Requirements

### Requirement: Roteamento por Bounded Context
O gateway DEVE encaminhar as requisições cujo caminho começa com `/lancamentos/` para a Lancamentos.Api e as que começam com `/posicao/` para a Posicao.Api. Método, demais segmentos do caminho, query string, corpo e cabeçalhos são preservados.

#### Scenario: Requisição para Lançamentos
- **WHEN** o gateway recebe `POST /lancamentos/contas` com um corpo JSON
- **THEN** a Lancamentos.Api recebe a requisição com o mesmo método, caminho, corpo e cabeçalhos
- **AND** o cliente recebe o status e o corpo devolvidos pela Lancamentos.Api

#### Scenario: Requisição para Posição Consolidada
- **WHEN** o gateway recebe `GET /posicao/contas/{id}/posicao?instante=2026-10-27T18:00:00Z`
- **THEN** a Posicao.Api recebe a requisição com o mesmo caminho e query string

### Requirement: Caminhos desconhecidos
O gateway DEVE responder `404` às requisições cujo caminho não pertence a nenhum Bounded Context, sem encaminhá-las a nenhum serviço.

#### Scenario: Caminho sem rota
- **WHEN** o gateway recebe `GET /qualquer-coisa`
- **THEN** a resposta é `404`
- **AND** nenhum serviço interno recebe a requisição

### Requirement: Endpoints internos não expostos
O gateway NÃO DEVE encaminhar requisições aos endpoints de saúde dos serviços internos. A saúde dos serviços é consultada pela rede interna, não pelo ponto de entrada público.

#### Scenario: Tentativa de acessar a saúde de um serviço pelo gateway
- **WHEN** o gateway recebe `GET /lancamentos/health/ready` ou `GET /posicao/health/ready`
- **THEN** a resposta é `404`
- **AND** a requisição não é encaminhada

### Requirement: Serviço de destino indisponível
Quando o serviço de destino de uma rota não responde, o gateway DEVE devolver um erro de gateway (`502` ou `504`) dentro de um tempo limite configurado. Ele não pode deixar a requisição presa indefinidamente nem afetar as rotas do outro Bounded Context.

#### Scenario: Lançamentos fora do ar
- **WHEN** a Lancamentos.Api está fora do ar e o gateway recebe uma requisição para `/lancamentos/...`
- **THEN** a resposta é `502` ou `504` dentro do tempo limite configurado

#### Scenario: Isolamento entre rotas
- **WHEN** a Lancamentos.Api está fora do ar e o gateway recebe uma requisição para `/posicao/...`
- **THEN** a requisição é encaminhada normalmente à Posicao.Api

### Requirement: Identificador de correlação
O gateway DEVE garantir que toda requisição encaminhada carregue o cabeçalho `X-Correlation-Id`. Se o cliente não enviar o cabeçalho, o gateway gera um novo. O mesmo valor volta na resposta.

#### Scenario: Cliente não envia correlação
- **WHEN** o gateway recebe uma requisição sem `X-Correlation-Id`
- **THEN** o serviço de destino recebe um `X-Correlation-Id` gerado pelo gateway
- **AND** a resposta ao cliente contém o mesmo valor

#### Scenario: Cliente envia correlação
- **WHEN** o gateway recebe uma requisição com `X-Correlation-Id` válido (até 64 caracteres, só letras, dígitos e hífen)
- **THEN** o mesmo valor é encaminhado ao serviço e devolvido na resposta

#### Scenario: Correlação inválida
- **WHEN** o gateway recebe um `X-Correlation-Id` fora desse formato
- **THEN** o valor é descartado e substituído por um novo, gerado pelo gateway, para evitar injeção em logs

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
