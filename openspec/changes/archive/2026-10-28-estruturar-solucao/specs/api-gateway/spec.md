# Spec Delta

## Purpose

O API Gateway é o único ponto de entrada externo do sistema. Ele encaminha cada requisição ao serviço do Bounded Context correspondente, para que os serviços internos nunca fiquem expostos diretamente.

## ADDED Requirements

### Requirement: Roteamento por Bounded Context
O gateway SHALL encaminhar as requisições cujo caminho começa com `/lancamentos/` para a Lancamentos.Api e as que começam com `/posicao/` para a Posicao.Api. Método, demais segmentos do caminho, query string, corpo e cabeçalhos são preservados.

#### Scenario: Requisição para Lançamentos
- **WHEN** o gateway recebe `POST /lancamentos/contas` com um corpo JSON
- **THEN** a Lancamentos.Api recebe a requisição com o mesmo método, caminho, corpo e cabeçalhos
- **AND** o cliente recebe o status e o corpo devolvidos pela Lancamentos.Api

#### Scenario: Requisição para Posição Consolidada
- **WHEN** o gateway recebe `GET /posicao/contas/{id}/posicao?instante=2026-10-27T18:00:00Z`
- **THEN** a Posicao.Api recebe a requisição com o mesmo caminho e query string

### Requirement: Caminhos desconhecidos
O gateway SHALL responder `404` às requisições cujo caminho não pertence a nenhum Bounded Context, sem encaminhá-las a nenhum serviço.

#### Scenario: Caminho sem rota
- **WHEN** o gateway recebe `GET /qualquer-coisa`
- **THEN** a resposta é `404`
- **AND** nenhum serviço interno recebe a requisição

### Requirement: Endpoints internos não expostos
O gateway SHALL não encaminhar requisições aos endpoints de saúde dos serviços internos. A saúde dos serviços é consultada pela rede interna, não pelo ponto de entrada público.

#### Scenario: Tentativa de acessar a saúde de um serviço pelo gateway
- **WHEN** o gateway recebe `GET /lancamentos/health/ready` ou `GET /posicao/health/ready`
- **THEN** a resposta é `404`
- **AND** a requisição não é encaminhada

### Requirement: Serviço de destino indisponível
Quando o serviço de destino de uma rota não responde, o gateway SHALL devolver um erro de gateway (`502` ou `504`) dentro de um tempo limite configurado. Ele não pode deixar a requisição presa indefinidamente nem afetar as rotas do outro Bounded Context.

#### Scenario: Lançamentos fora do ar
- **WHEN** a Lancamentos.Api está fora do ar e o gateway recebe uma requisição para `/lancamentos/...`
- **THEN** a resposta é `502` ou `504` dentro do tempo limite configurado

#### Scenario: Isolamento entre rotas
- **WHEN** a Lancamentos.Api está fora do ar e o gateway recebe uma requisição para `/posicao/...`
- **THEN** a requisição é encaminhada normalmente à Posicao.Api

### Requirement: Identificador de correlação
O gateway SHALL garantir que toda requisição encaminhada carregue o cabeçalho `X-Correlation-Id`. Se o cliente não enviar o cabeçalho, o gateway gera um novo. O mesmo valor volta na resposta.

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
