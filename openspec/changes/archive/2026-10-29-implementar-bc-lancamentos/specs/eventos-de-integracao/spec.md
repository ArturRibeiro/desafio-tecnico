# Spec Delta

## Purpose

Define os eventos que Lançamentos publica para outros Bounded Contexts (Published Language) e as garantias de entrega deles. Tudo o que foi registrado chega ao broker, sem depender de ele estar disponível na hora da operação e sem expor o modelo interno nem dados cadastrais do cliente.

## ADDED Requirements

### Requirement: Evento de conta aberta
Toda abertura de conta SHALL publicar um único `ContaAberta` versão `1`, com `eventoId`, `tipoEvento`, `versao`, `contaId`, `titularId`, `sequencia = 0`, `moeda`, `dataAbertura` e `correlacaoId`.

#### Scenario: Conta aberta
- **WHEN** uma conta é aberta
- **THEN** é publicado um `ContaAberta` v1 com o `contaId` da conta, o `titularId` do titular e `sequencia = 0`

#### Scenario: Abertura repetida com a mesma chave
- **WHEN** a abertura é repetida com a mesma `Idempotency-Key`
- **THEN** nenhum `ContaAberta` adicional é publicado

### Requirement: Evento de movimentação registrada
Todo crédito, débito ou estorno aceito SHALL publicar um único `MovimentacaoRegistrada` versão `1`, com:
- `eventoId`, `tipoEvento`, `versao`, `contaId`, `lancamentoId`;
- `sequencia`, `tipo`, `valor`, `moeda`;
- `dataCompetencia`, `dataRegistro`, `lancamentoEstornadoId` e `correlacaoId`.

#### Scenario: Débito aceito
- **WHEN** um débito de `30.00` é aceito com `sequencia = 3`
- **THEN** é publicado um `MovimentacaoRegistrada` v1 com `tipo = "Debito"`, `valor = 30.00`, `sequencia = 3` e `lancamentoEstornadoId = null`

#### Scenario: Estorno aceito
- **WHEN** um estorno é aceito
- **THEN** o evento tem `tipo = "Estorno"` e `lancamentoEstornadoId` igual ao lançamento original

#### Scenario: Operação recusada
- **WHEN** um débito é recusado por saldo insuficiente
- **THEN** nenhum evento é publicado

### Requirement: Evento gravado junto com a operação
O evento SHALL ser gravado na mesma transação que a operação que o originou. Não pode existir operação confirmada sem o seu evento, nem evento de uma operação que não foi confirmada.

#### Scenario: Falha ao confirmar a transação
- **WHEN** a gravação de um lançamento falha antes de ser confirmada
- **THEN** nem o lançamento nem o seu evento ficam registrados

### Requirement: Entrega pelo menos uma vez, tolerante ao broker
Os eventos SHALL ser entregues ao broker pelo menos uma vez. Se o broker estiver indisponível, a operação continua sendo aceita normalmente, e os eventos pendentes são entregues automaticamente quando ele volta, sem intervenção manual.

#### Scenario: Broker fora durante as operações
- **WHEN** o RabbitMQ está indisponível e o titular registra um crédito
- **THEN** a resposta é `201`
- **AND** quando o RabbitMQ volta, o `MovimentacaoRegistrada` correspondente é publicado

#### Scenario: Consumidor recebe o evento duas vezes
- **WHEN** o mesmo evento é entregue mais de uma vez
- **THEN** todas as entregas têm o mesmo `eventoId`, para que o consumidor possa descartar as repetições

### Requirement: Contrato sem dados cadastrais e com titular mínimo
Os eventos SHALL carregar apenas identificadores opacos, valores e datas, sem nome, documento nem contato do cliente. O `titularId` aparece somente em `ContaAberta`, nunca em `MovimentacaoRegistrada`.

#### Scenario: Conteúdo de uma movimentação
- **WHEN** um `MovimentacaoRegistrada` é publicado
- **THEN** ele não contém `titularId` nem a descrição do lançamento

### Requirement: Contrato versionado e independente do modelo interno
Os eventos SHALL ser definidos no projeto de contratos de integração, separado do Domínio. Uma mudança incompatível no contrato exige uma nova versão do evento, que convive com a anterior.

#### Scenario: Refatoração do Domínio
- **WHEN** um tipo interno do Domínio de Lançamentos é renomeado
- **THEN** o contrato publicado não muda
