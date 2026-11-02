# Spec Delta

## Purpose

Define como um cliente prova quem é ao chamar o sistema e quais permissões carrega. A identidade do titular vem sempre de um token assinado por um emissor confiável, nunca de um dado informado na requisição.

## ADDED Requirements

### Requirement: Token obrigatório nas operações de Lançamentos
Toda requisição às operações de negócio da Lancamentos.Api SHALL apresentar um token JWT no cabeçalho `Authorization: Bearer`. Sem token, ou com token inválido, a resposta é `401`, e nenhuma operação é executada.

#### Scenario: Requisição sem token
- **WHEN** um cliente chama `POST /lancamentos/contas` sem o cabeçalho `Authorization`
- **THEN** a resposta é `401`
- **AND** nenhuma conta é criada

#### Scenario: Token com assinatura inválida
- **WHEN** o token foi assinado com uma chave diferente da configurada
- **THEN** a resposta é `401`

#### Scenario: Token expirado
- **WHEN** o token tem `exp` no passado
- **THEN** a resposta é `401`

#### Scenario: Token de outro emissor ou de outra audiência
- **WHEN** o `iss` ou o `aud` do token não corresponde ao configurado
- **THEN** a resposta é `401`

### Requirement: Escopo de escrita em Lançamentos
As operações de negócio da Lancamentos.Api SHALL exigir que o token carregue o escopo `lancamentos:escrever` na claim `scope`, que lista escopos separados por espaço. Um token válido sem esse escopo recebe `403`.

#### Scenario: Token só com escopo de consulta
- **WHEN** um token válido com `scope = "posicao:ler"` chama `POST /lancamentos/contas/{contaId}/creditos`
- **THEN** a resposta é `403`
- **AND** nenhum lançamento é criado

#### Scenario: Token com vários escopos
- **WHEN** um token válido com `scope = "posicao:ler lancamentos:escrever"` chama uma operação de Lançamentos
- **THEN** a autorização por escopo é concedida

### Requirement: Titular identificado pelo token
O `TitularId` do cliente SHALL ser o valor da claim `sub` do token. Um token sem `sub`, ou com `sub` vazio ou maior que 200 caracteres, é tratado como inválido (`401`). Nenhum campo do corpo, da rota, da query string ou de outro cabeçalho pode definir ou substituir o titular.

#### Scenario: Token sem sub
- **WHEN** um token válido e com o escopo correto não tem a claim `sub`
- **THEN** a resposta é `401`

#### Scenario: Tentativa de informar outro titular
- **WHEN** o corpo da requisição contém um campo `titularId` diferente do `sub` do token
- **THEN** o campo é ignorado e a operação usa o `sub` do token

### Requirement: Saúde sem autenticação
Os endpoints `/health/live` e `/health/ready` SHALL continuar acessíveis sem token, para que o orquestrador consiga sondar o serviço.

#### Scenario: Sonda de prontidão sem token
- **WHEN** `GET /health/ready` é chamado na Lancamentos.Api sem `Authorization`
- **THEN** a resposta não é `401`
