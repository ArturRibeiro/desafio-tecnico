# Spec Delta

## MODIFIED Requirements

### Requirement: Dependências obrigatórias por serviço
Cada serviço SHALL verificar na prontidão exatamente as dependências de que precisa para funcionar:
- Lancamentos.Api: o seu PostgreSQL, o esquema do banco atualizado (`migracoes`) e o RabbitMQ;
- Posicao.Worker: o seu PostgreSQL e o RabbitMQ;
- Posicao.Api: o seu PostgreSQL;
- API Gateway: nenhuma.

#### Scenario: Posicao.Api com cache fora do ar
- **WHEN** o Redis está inacessível, o PostgreSQL de Posição está acessível e `GET /health/ready` é chamado na Posicao.Api
- **THEN** a resposta é `200`, porque o cache é opcional
- **AND** o corpo informa o cache como degradado

#### Scenario: Lancamentos.Api sem broker
- **WHEN** o RabbitMQ está inacessível e `GET /health/ready` é chamado na Lancamentos.Api
- **THEN** a resposta é `503`

#### Scenario: Banco de um contexto não afeta o outro
- **WHEN** o PostgreSQL de Lançamentos está inacessível e `GET /health/ready` é chamado na Posicao.Api
- **THEN** a resposta não é afetada pelo banco de Lançamentos

#### Scenario: Lancamentos.Api com esquema desatualizado
- **WHEN** o PostgreSQL de Lançamentos está acessível, mas as migrações do esquema ainda não foram aplicadas
- **THEN** `GET /health/ready` na Lancamentos.Api responde `503` com a dependência `migracoes` como `Unhealthy`
- **AND** passa a responder `200` depois que as migrações são aplicadas, sem reiniciar o serviço
