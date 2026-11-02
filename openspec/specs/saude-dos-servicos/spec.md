# saude-dos-servicos Specification

## Purpose

Permite que o orquestrador e quem opera o sistema saibam se cada serviço está vivo e se está pronto para receber tráfego. Assim, uma instância com problema ou sem acesso às suas dependências pode ser retirada do balanceamento sem derrubar o sistema.

## Requirements

### Requirement: Endpoint de vida
Todo serviço da solução (API Gateway, Lancamentos.Api, Posicao.Api e Posicao.Worker) DEVE expor `GET /health/live`. O endpoint responde `200` enquanto o processo estiver em execução e não consulta nenhuma dependência externa.

#### Scenario: Processo em execução
- **WHEN** `GET /health/live` é chamado em um serviço em execução
- **THEN** a resposta é `200`

#### Scenario: Dependência fora do ar não afeta a vida
- **WHEN** uma dependência do serviço (banco, broker ou cache) está indisponível e `GET /health/live` é chamado
- **THEN** a resposta continua sendo `200`

### Requirement: Endpoint de prontidão
Todo serviço DEVE expor `GET /health/ready`. O endpoint responde `200` somente quando todas as dependências obrigatórias do serviço estão acessíveis e `503` quando ao menos uma não está.

#### Scenario: Dependências acessíveis
- **WHEN** todas as dependências obrigatórias do serviço estão acessíveis e `GET /health/ready` é chamado
- **THEN** a resposta é `200`

#### Scenario: Dependência obrigatória fora do ar
- **WHEN** uma dependência obrigatória do serviço está inacessível e `GET /health/ready` é chamado
- **THEN** a resposta é `503`

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

### Requirement: Detalhe da prontidão sem dados sensíveis
A resposta de `GET /health/ready` DEVE informar em JSON o estado geral e o estado de cada dependência verificada (`Healthy`, `Degraded` ou `Unhealthy`). Ela não pode expor connection strings, credenciais, hosts internos nem mensagens de exceção.

#### Scenario: Corpo da prontidão
- **WHEN** `GET /health/ready` é chamado
- **THEN** o corpo é JSON com o estado geral e uma entrada por dependência, identificada por nome lógico (por exemplo, `postgres`, `rabbitmq`, `redis`)

#### Scenario: Falha não vaza detalhes
- **WHEN** uma dependência falha com uma exceção que contém host ou credencial
- **THEN** o corpo da resposta não contém o texto da exceção, o host nem a credencial
