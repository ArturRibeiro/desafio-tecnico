# observabilidade Specification

## Purpose

Permite seguir uma operação de ponta a ponta, do gateway até a Posição Consolidada, e diagnosticar falhas pelos logs, sem que logs e traces exponham dados sensíveis.

## Requirements

### Requirement: Logs estruturados
Todo serviço DEVE escrever os logs em JSON, uma linha por registro, com nível, categoria, mensagem e os campos estruturados da mensagem.

#### Scenario: Linha de log
- **WHEN** um serviço registra um evento
- **THEN** a linha é um objeto JSON válido com o nível, a categoria e a mensagem

### Requirement: Correlação nos logs
Os logs escritos durante uma requisição ou durante o consumo de um evento DEVEM trazer o identificador de correlação e o identificador do trace.

#### Scenario: Log durante uma requisição
- **GIVEN** uma requisição com `X-Correlation-Id: abc-123`
- **WHEN** o serviço registra um log ao atendê-la
- **THEN** o registro contém `abc-123` e o identificador do trace

#### Scenario: Log durante o consumo de um evento
- **WHEN** o Posicao.Worker registra um log ao consumir um evento
- **THEN** o registro contém o `correlacaoId` do evento

### Requirement: Logs sem dados sensíveis
Os logs NÃO DEVEM conter tokens, o titular, valores nem descrições de lançamentos, mesmo nos registros de erro.

#### Scenario: Fluxo completo
- **GIVEN** um cliente que abre uma conta, credita, debita, estorna e consulta a posição
- **WHEN** os logs de todos os serviços envolvidos são examinados
- **THEN** nenhum registro contém o token, o titular, os valores nem as descrições usados

#### Scenario: Requisição recusada pela autenticação
- **WHEN** uma requisição chega com um token inválido
- **THEN** os logs não contêm o token

### Requirement: Rastreamento ponta a ponta
Uma requisição DEVE produzir um único trace, que liga o gateway, o serviço, o banco, a publicação do evento e o seu consumo no Posicao.Worker. Isso vale mesmo quando a entrega do evento é adiada pelo Outbox.

#### Scenario: Débito até a projeção
- **WHEN** um débito é registrado pelo gateway
- **THEN** a projeção do débito no Posicao.Worker pertence ao mesmo trace da requisição HTTP

#### Scenario: Broker fora durante o registro
- **GIVEN** que o RabbitMQ estava fora do ar quando o débito foi registrado
- **WHEN** o broker volta e o evento é consumido
- **THEN** o consumo ainda pertence ao trace da requisição original

### Requirement: Correlação nos traces
Os spans de uma requisição e do consumo dos seus eventos DEVEM carregar o identificador de correlação, para que um trace possa ser achado a partir do `X-Correlation-Id` devolvido ao cliente.

#### Scenario: Busca pelo identificador de correlação
- **GIVEN** a resposta de um crédito com `X-Correlation-Id: abc-123`
- **WHEN** os spans são filtrados pela correlação `abc-123`
- **THEN** aparecem o span da requisição e o do consumo do evento no Posicao.Worker

### Requirement: Exportação opcional
Os traces DEVEM ser exportados por OTLP somente quando um destino estiver configurado. Sem destino, o serviço funciona normalmente, sem erros por causa disso.

#### Scenario: Sem destino configurado
- **GIVEN** nenhum destino OTLP configurado
- **WHEN** o serviço atende requisições
- **THEN** as respostas não mudam e nenhum erro de exportação é registrado
