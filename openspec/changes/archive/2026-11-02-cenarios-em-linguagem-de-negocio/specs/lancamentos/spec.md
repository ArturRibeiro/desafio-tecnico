# Spec Delta

## MODIFIED Requirements

### Requirement: Registrar crédito
`POST /lancamentos/contas/{contaId}/creditos`, com `valor`, `descricao` e, opcionalmente, `dataCompetencia`, DEVE criar um lançamento de crédito e aumentar o saldo disponível no mesmo valor. A resposta é `201` com o lançamento criado e o saldo disponível após a operação.

#### Scenario: Crédito em conta ativa
- **GIVEN** uma conta com saldo de R$ 0,00
- **WHEN** o titular credita R$ 100,00
- **THEN** o crédito de R$ 100,00 é registrado como o primeiro lançamento da conta
- **AND** o saldo passa a ser R$ 100,00

### Requirement: Registrar débito
`POST /lancamentos/contas/{contaId}/debitos` DEVE criar um lançamento de débito e reduzir o saldo disponível, desde que o saldo resultante não fique abaixo de `-limite`. Caso contrário, a resposta é `422` com código `saldo-insuficiente`, e nada muda.

#### Scenario: Débito dentro do saldo
- **GIVEN** uma conta com saldo de R$ 100,00 e sem limite
- **WHEN** o titular debita R$ 30,00
- **THEN** o débito é registrado
- **AND** o saldo passa a ser R$ 70,00

#### Scenario: Débito de todo o saldo
- **GIVEN** uma conta com saldo de R$ 100,00 e sem limite
- **WHEN** o titular debita R$ 100,00
- **THEN** o débito é registrado
- **AND** o saldo passa a ser R$ 0,00

#### Scenario: Débito acima do saldo
- **GIVEN** uma conta com saldo de R$ 100,00 e sem limite
- **WHEN** o titular tenta debitar R$ 100,01
- **THEN** o débito é recusado por saldo insuficiente
- **AND** o saldo continua R$ 100,00 e nenhum lançamento é registrado

### Requirement: Conta precisa estar ativa
Uma conta cuja situação não é `Ativa` DEVE recusar qualquer lançamento com `422` e código `conta-inativa`.

#### Scenario: Crédito em conta bloqueada
- **GIVEN** uma conta bloqueada
- **WHEN** o titular tenta creditar R$ 10,00
- **THEN** o crédito é recusado porque a conta não está ativa

### Requirement: Valor do lançamento
O `valor` DEVE ser maior que zero, ter no máximo duas casas decimais e no máximo `999999999999.99`. A moeda é sempre `BRL`. Um valor fora dessas regras é recusado com `400` e código `valor-invalido`, sem arredondamento.

#### Scenario: Valor zero ou negativo
- **WHEN** o titular tenta creditar R$ 0,00 ou R$ -10,00
- **THEN** o crédito é recusado por valor inválido

#### Scenario: Mais de duas casas decimais
- **WHEN** o titular tenta creditar R$ 10,005
- **THEN** o crédito é recusado por valor inválido, sem arredondamento

### Requirement: Descrição do lançamento
A `descricao` DEVE ser obrigatória e ter entre 1 e 200 caracteres, sem contar espaços nas pontas. Fora disso, a resposta é `400` com código `descricao-invalida`.

#### Scenario: Descrição longa demais
- **WHEN** o titular tenta creditar R$ 10,00 com uma descrição de 201 caracteres
- **THEN** o crédito é recusado por descrição inválida

### Requirement: Data de competência
A `dataCompetencia` DEVE ser um instante com fuso, entre o início do dia corrente em UTC e o instante atual. Quando omitida, vale o instante do registro. Fora desse intervalo, a resposta é `422` com código `data-competencia-invalida`. Assim, dias já fechados nunca mudam.

#### Scenario: Competência omitida
- **WHEN** o titular credita R$ 10,00 sem informar a data de competência
- **THEN** o crédito vale a partir do instante em que foi registrado

#### Scenario: Competência no futuro
- **WHEN** o titular tenta creditar R$ 10,00 com competência daqui a uma hora
- **THEN** o crédito é recusado por data de competência inválida

#### Scenario: Competência em dia anterior
- **WHEN** o titular tenta creditar R$ 10,00 com competência no dia anterior
- **THEN** o crédito é recusado por data de competência inválida

### Requirement: Sequência sem lacunas
Cada lançamento de uma conta DEVE receber uma `sequencia` igual à anterior mais um, começando em `1`. Operações recusadas não consomem sequência.

#### Scenario: Débito recusado entre dois créditos
- **GIVEN** uma conta com saldo de R$ 0,00 e sem limite
- **WHEN** o titular credita R$ 10,00, tenta debitar R$ 50,00 e credita mais R$ 10,00
- **THEN** os dois créditos são o primeiro e o segundo lançamento da conta

### Requirement: Estornar lançamento
`POST /lancamentos/contas/{contaId}/lancamentos/{lancamentoId}/estornos` DEVE criar um lançamento `Estorno`, com o mesmo valor do original, `lancamentoEstornadoId` apontando para ele e efeito inverso no saldo. O estorno obedece às mesmas regras de conta ativa e de saldo. A descrição é opcional; se omitida, o sistema gera uma.

#### Scenario: Estorno de um débito
- **GIVEN** uma conta com saldo de R$ 70,00 depois de um débito de R$ 30,00
- **WHEN** o titular estorna esse débito
- **THEN** o estorno de R$ 30,00 é registrado
- **AND** o saldo volta a ser R$ 100,00

#### Scenario: Estorno de crédito sem saldo
- **GIVEN** uma conta sem limite que recebeu um crédito de R$ 100,00 e ficou com saldo de R$ 40,00
- **WHEN** o titular tenta estornar esse crédito
- **THEN** o estorno é recusado por saldo insuficiente

### Requirement: Estorno único
Um lançamento DEVE ser estornado no máximo uma vez, e um estorno não pode ser estornado. Um segundo estorno do mesmo lançamento recebe `409` com código `lancamento-ja-estornado`. O estorno de um estorno recebe `422` com código `estorno-nao-estornavel`.

#### Scenario: Segundo estorno com outra chave
- **GIVEN** um débito já estornado
- **WHEN** o titular faz um novo pedido de estorno desse débito
- **THEN** o estorno é recusado porque o lançamento já foi estornado
- **AND** o saldo não muda

#### Scenario: Estorno de um estorno
- **GIVEN** um débito já estornado
- **WHEN** o titular tenta estornar o estorno
- **THEN** o pedido é recusado porque um estorno não pode ser estornado

### Requirement: Lançamento a estornar precisa existir na conta
Quando o `lancamentoId` não existe ou pertence a outra conta, o estorno DEVE ser recusado com `404` e código `lancamento-nao-encontrado`.

#### Scenario: Lançamento de outra conta
- **GIVEN** que o titular tem as contas C1 e C2 e um débito registrado em C2
- **WHEN** ele tenta estornar esse débito pela conta C1
- **THEN** o estorno é recusado porque o lançamento não foi encontrado

### Requirement: Lançamentos imutáveis
O sistema NÃO DEVE oferecer nenhuma forma de alterar ou excluir um lançamento. A única correção possível é o estorno.

#### Scenario: Tentativa de excluir um lançamento
- **GIVEN** um débito registrado
- **WHEN** o titular tenta alterar ou excluir esse débito
- **THEN** o sistema não oferece essa possibilidade
- **AND** o lançamento e o saldo permanecem inalterados

### Requirement: Idempotência dos lançamentos
Créditos, débitos e estornos DEVEM exigir o cabeçalho `Idempotency-Key`, com 1 a 100 caracteres visíveis e única por conta. A repetição com a mesma chave e o mesmo conteúdo devolve a resposta original sem criar outro lançamento. Chave ausente ou inválida recebe `400` (`chave-idempotencia-obrigatoria` ou `chave-idempotencia-invalida`).

#### Scenario: Repetição após timeout
- **GIVEN** uma conta com saldo de R$ 100,00
- **WHEN** o titular repete o mesmo pedido de débito de R$ 30,00, sem saber se o primeiro chegou
- **THEN** ele recebe o mesmo débito da primeira vez
- **AND** a conta foi debitada uma única vez e o saldo é R$ 70,00

#### Scenario: Mesma chave com outro conteúdo
- **GIVEN** um débito de R$ 30,00 registrado
- **WHEN** o titular reaproveita o mesmo pedido para debitar R$ 50,00
- **THEN** o pedido é recusado por conflito com o pedido original
- **AND** nada muda

#### Scenario: Repetição de uma operação recusada
- **GIVEN** que um débito de R$ 50,00 foi recusado por saldo insuficiente
- **WHEN** o titular recebe um crédito de R$ 50,00 e repete o mesmo pedido de débito
- **THEN** o débito é avaliado de novo e registrado, porque operações recusadas não ficam registradas

### Requirement: Operações simultâneas na mesma conta
Operações simultâneas sobre a mesma conta DEVEM produzir o mesmo resultado de alguma execução em série. O saldo nunca ultrapassa o limite, nenhuma operação aceita se perde e a sequência continua sem lacunas.

#### Scenario: Débitos concorrentes
- **GIVEN** uma conta com saldo de R$ 50,00 e sem limite
- **WHEN** o titular faz 10 débitos de R$ 10,00 ao mesmo tempo
- **THEN** exatamente 5 débitos são registrados e os outros 5 são recusados por saldo insuficiente
- **AND** o saldo final é R$ 0,00 e os débitos registrados ficam em sequência, sem lacunas

### Requirement: Formato dos erros
Toda resposta de erro da Lancamentos.Api DEVE usar `application/problem+json` (Problem Details), com o campo `codigo` estável e legível por máquina. Ela não pode conter stack trace, SQL, nomes internos de tipos nem dados de outros clientes. Um corpo JSON malformado ou com tipos errados recebe `400` com código `requisicao-invalida`.

#### Scenario: JSON malformado
- **WHEN** o titular envia um pedido de crédito ilegível
- **THEN** o pedido é recusado como inválido, com o motivo identificado

#### Scenario: Falha inesperada
- **WHEN** ocorre uma falha técnica imprevista durante um crédito
- **THEN** o crédito é recusado por erro interno, sem revelar detalhes técnicos
