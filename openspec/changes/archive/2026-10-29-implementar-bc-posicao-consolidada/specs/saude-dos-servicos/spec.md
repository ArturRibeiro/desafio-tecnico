# Spec Delta

## MODIFIED Requirements

### Requirement: Dependências obrigatórias por serviço
Cada serviço DEVE verificar na prontidão exatamente as dependências de que precisa para funcionar:
- Lancamentos.Api: o seu PostgreSQL, o esquema do banco atualizado (`migracoes`) e o RabbitMQ;
- Posicao.Worker: o seu PostgreSQL, o esquema do banco atualizado (`migracoes`) e o RabbitMQ;
- Posicao.Api: o seu PostgreSQL e o esquema do banco atualizado (`migracoes`);
- API Gateway: nenhuma.

#### Scenario: Posicao.Api com cache fora do ar
- **GIVEN** que o Redis está inacessível e o PostgreSQL de Posição está acessível
- **WHEN** `GET /health/ready` é chamado na Posicao.Api
- **THEN** a resposta é `200`, porque o cache é opcional
- **AND** o corpo informa o cache como degradado

#### Scenario: Lancamentos.Api sem broker
- **GIVEN** que o RabbitMQ está inacessível
- **WHEN** `GET /health/ready` é chamado na Lancamentos.Api
- **THEN** a resposta é `503`

#### Scenario: Banco de um contexto não afeta o outro
- **GIVEN** que o PostgreSQL de Lançamentos está inacessível
- **WHEN** `GET /health/ready` é chamado na Posicao.Api
- **THEN** a resposta não é afetada pelo banco de Lançamentos

#### Scenario: Lancamentos.Api com esquema desatualizado
- **GIVEN** que o PostgreSQL de Lançamentos está acessível, mas as migrações do esquema ainda não foram aplicadas
- **WHEN** `GET /health/ready` é chamado na Lancamentos.Api
- **THEN** a resposta é `503` com a dependência `migracoes` como `Unhealthy`
- **AND** passa a ser `200` depois que as migrações são aplicadas, sem reiniciar o serviço

#### Scenario: Serviços de Posição com esquema desatualizado
- **GIVEN** que o PostgreSQL de Posição está acessível, mas as migrações do esquema ainda não foram aplicadas
- **WHEN** `GET /health/ready` é chamado na Posicao.Api ou no Posicao.Worker
- **THEN** a resposta é `503` com a dependência `migracoes` como `Unhealthy`
- **AND** passa a ser `200` depois que as migrações são aplicadas, sem reiniciar o serviço
