# Spec Delta

## ADDED Requirements

### Requirement: Token obrigatório nas consultas de Posição
Toda requisição às consultas da Posicao.Api DEVE apresentar um token JWT no cabeçalho `Authorization: Bearer`. Sem token, ou com token inválido, a resposta é `401`.

#### Scenario: Consulta sem token
- **WHEN** a posição de uma conta é consultada sem o cabeçalho `Authorization`
- **THEN** a resposta é `401`

#### Scenario: Token expirado
- **GIVEN** um token cuja validade já terminou
- **WHEN** ele é usado para consultar a posição
- **THEN** a resposta é `401`

### Requirement: Escopo de leitura em Posição
As consultas da Posicao.Api DEVEM exigir que o token carregue o escopo `posicao:ler` na claim `scope`. Um token válido sem esse escopo recebe `403`.

#### Scenario: Token só de escrita
- **GIVEN** um token válido apenas com o escopo `lancamentos:escrever`
- **WHEN** ele é usado para consultar a posição
- **THEN** a resposta é `403`
