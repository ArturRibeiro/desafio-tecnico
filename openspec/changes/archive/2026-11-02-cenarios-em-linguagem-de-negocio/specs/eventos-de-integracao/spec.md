# Spec Delta

## MODIFIED Requirements

### Requirement: Evento de conta aberta
Toda abertura de conta DEVE publicar um único `ContaAberta` versão `1`, com `eventoId`, `tipoEvento`, `versao`, `contaId`, `titularId`, `sequencia = 0`, `moeda`, `dataAbertura` e `correlacaoId`.

#### Scenario: Conta aberta
- **WHEN** um cliente abre uma conta
- **THEN** Lançamentos avisa uma única vez que a conta foi aberta, informando a conta, o titular e a sequência 0

#### Scenario: Abertura repetida com a mesma chave
- **WHEN** o cliente repete o mesmo pedido de abertura
- **THEN** Lançamentos não avisa a abertura de novo

### Requirement: Evento de movimentação registrada
Todo crédito, débito ou estorno aceito DEVE publicar um único `MovimentacaoRegistrada` versão `1`, com:
- `eventoId`, `tipoEvento`, `versao`, `contaId`, `lancamentoId`;
- `sequencia`, `tipo`, `valor`, `moeda`;
- `dataCompetencia`, `dataRegistro`, `lancamentoEstornadoId` e `correlacaoId`.

#### Scenario: Débito aceito
- **GIVEN** uma conta com dois lançamentos e saldo de R$ 100,00
- **WHEN** o titular debita R$ 30,00
- **THEN** Lançamentos avisa a movimentação: débito de R$ 30,00, terceiro lançamento da conta, sem lançamento estornado

#### Scenario: Estorno aceito
- **GIVEN** um débito registrado
- **WHEN** o titular estorna esse débito
- **THEN** Lançamentos avisa a movimentação como estorno, indicando o débito estornado

#### Scenario: Operação recusada
- **WHEN** um débito é recusado por saldo insuficiente
- **THEN** Lançamentos não avisa nenhuma movimentação

### Requirement: Evento gravado junto com a operação
O evento DEVE ser gravado na mesma transação que a operação que o originou. Não pode existir operação confirmada sem o seu evento, nem evento de uma operação que não foi confirmada.

#### Scenario: Falha ao confirmar a transação
- **WHEN** a gravação de um crédito falha antes de ser concluída
- **THEN** o crédito não fica registrado
- **AND** Lançamentos não avisa nenhuma movimentação

### Requirement: Entrega pelo menos uma vez, tolerante ao broker
Os eventos DEVEM ser entregues ao broker pelo menos uma vez. Se o broker estiver indisponível, a operação continua sendo aceita normalmente, e os eventos pendentes são entregues automaticamente quando ele volta, sem intervenção manual.

#### Scenario: Broker fora durante as operações
- **WHEN** a entrega de avisos entre os contextos está indisponível e o titular credita R$ 25,00
- **THEN** o crédito é registrado normalmente
- **AND** quando a entrega volta, Lançamentos avisa a movimentação desse crédito

#### Scenario: Consumidor recebe o evento duas vezes
- **WHEN** o mesmo aviso de movimentação é entregue mais de uma vez
- **THEN** todas as entregas têm a mesma identificação, para que quem recebe possa descartar as repetições

### Requirement: Contrato sem dados cadastrais e com titular mínimo
Os eventos DEVEM carregar apenas identificadores opacos, valores e datas, sem nome, documento nem contato do cliente. O `titularId` aparece somente em `ContaAberta`, nunca em `MovimentacaoRegistrada`.

#### Scenario: Conteúdo de uma movimentação
- **WHEN** Lançamentos avisa uma movimentação
- **THEN** o aviso não identifica o titular nem traz a descrição do lançamento

### Requirement: Contrato versionado e independente do modelo interno
Os eventos DEVEM ser definidos no projeto de contratos de integração, separado do Domínio. Uma mudança incompatível no contrato exige uma nova versão do evento, que convive com a anterior.

#### Scenario: Refatoração do Domínio
- **WHEN** o modelo interno de Lançamentos evolui
- **THEN** os avisos continuam com as mesmas informações da versão 1
