# Spec Delta

## Purpose

Registro de créditos, débitos e estornos nas contas, com consistência forte. Lançamentos é a fonte da verdade financeira: nada pode se perder, duplicar ou deixar o saldo fora das regras, mesmo com requisições repetidas ou simultâneas.

## ADDED Requirements

### Requirement: Registrar crédito
`POST /lancamentos/contas/{contaId}/creditos`, com `valor`, `descricao` e, opcionalmente, `dataCompetencia`, SHALL criar um lançamento de crédito e aumentar o saldo disponível no mesmo valor. A resposta é `201` com o lançamento criado e o saldo disponível após a operação.

#### Scenario: Crédito em conta ativa
- **WHEN** o titular credita `100.00` numa conta com saldo `0.00`
- **THEN** a resposta é `201` com `tipo = "Credito"`, `valor = 100.00`, `sequencia = 1` e `saldoDisponivel = 100.00`

### Requirement: Registrar débito
`POST /lancamentos/contas/{contaId}/debitos` SHALL criar um lançamento de débito e reduzir o saldo disponível, desde que o saldo resultante não fique abaixo de `-limite`. Caso contrário, a resposta é `422` com código `saldo-insuficiente`, e nada muda.

#### Scenario: Débito dentro do saldo
- **WHEN** o titular debita `30.00` de uma conta com saldo `100.00` e limite `0.00`
- **THEN** a resposta é `201` com `tipo = "Debito"` e `saldoDisponivel = 70.00`

#### Scenario: Débito de todo o saldo
- **WHEN** o titular debita exatamente o saldo disponível, numa conta com limite `0.00`
- **THEN** a resposta é `201` com `saldoDisponivel = 0.00`

#### Scenario: Débito acima do saldo
- **WHEN** o titular debita `100.01` de uma conta com saldo `100.00` e limite `0.00`
- **THEN** a resposta é `422` com código `saldo-insuficiente`
- **AND** o saldo continua `100.00` e nenhum lançamento é criado

### Requirement: Conta precisa estar ativa
Uma conta cuja situação não é `Ativa` SHALL recusar qualquer lançamento com `422` e código `conta-inativa`.

#### Scenario: Crédito em conta bloqueada
- **WHEN** a conta está `Bloqueada` e o titular tenta creditar
- **THEN** a resposta é `422` com código `conta-inativa`

### Requirement: Valor do lançamento
O `valor` SHALL ser maior que zero, ter no máximo duas casas decimais e no máximo `999999999999.99`. A moeda é sempre `BRL`. Um valor fora dessas regras é recusado com `400` e código `valor-invalido`, sem arredondamento.

#### Scenario: Valor zero ou negativo
- **WHEN** o `valor` enviado é `0` ou `-10.00`
- **THEN** a resposta é `400` com código `valor-invalido`

#### Scenario: Mais de duas casas decimais
- **WHEN** o `valor` enviado é `10.005`
- **THEN** a resposta é `400` com código `valor-invalido` e nada é arredondado

### Requirement: Descrição do lançamento
A `descricao` SHALL ser obrigatória e ter entre 1 e 200 caracteres, sem contar espaços nas pontas. Fora disso, a resposta é `400` com código `descricao-invalida`.

#### Scenario: Descrição longa demais
- **WHEN** a `descricao` tem 201 caracteres
- **THEN** a resposta é `400` com código `descricao-invalida`

### Requirement: Data de competência
A `dataCompetencia` SHALL ser um instante com fuso, entre o início do dia corrente em UTC e o instante atual. Quando omitida, vale o instante do registro. Fora desse intervalo, a resposta é `422` com código `data-competencia-invalida`. Assim, dias já fechados nunca mudam.

#### Scenario: Competência omitida
- **WHEN** o crédito é enviado sem `dataCompetencia`
- **THEN** o lançamento tem `dataCompetencia` igual ao instante do registro

#### Scenario: Competência no futuro
- **WHEN** a `dataCompetencia` está uma hora à frente do instante atual
- **THEN** a resposta é `422` com código `data-competencia-invalida`

#### Scenario: Competência em dia anterior
- **WHEN** a `dataCompetencia` é anterior ao início do dia corrente em UTC
- **THEN** a resposta é `422` com código `data-competencia-invalida`

### Requirement: Sequência sem lacunas
Cada lançamento de uma conta SHALL receber uma `sequencia` igual à anterior mais um, começando em `1`. Operações recusadas não consomem sequência.

#### Scenario: Débito recusado entre dois créditos
- **WHEN** o titular registra um crédito, tenta um débito que é recusado por saldo insuficiente e registra outro crédito
- **THEN** os créditos têm `sequencia` `1` e `2`

### Requirement: Estornar lançamento
`POST /lancamentos/contas/{contaId}/lancamentos/{lancamentoId}/estornos` SHALL criar um lançamento `Estorno`, com o mesmo valor do original, `lancamentoEstornadoId` apontando para ele e efeito inverso no saldo. O estorno obedece às mesmas regras de conta ativa e de saldo. A descrição é opcional; se omitida, o sistema gera uma.

#### Scenario: Estorno de um débito
- **WHEN** o titular estorna um débito de `30.00` numa conta com saldo `70.00`
- **THEN** a resposta é `201` com `tipo = "Estorno"`, `valor = 30.00` e `saldoDisponivel = 100.00`

#### Scenario: Estorno de crédito sem saldo
- **WHEN** o titular estorna um crédito de `100.00` numa conta com saldo `40.00` e limite `0.00`
- **THEN** a resposta é `422` com código `saldo-insuficiente`

### Requirement: Estorno único
Um lançamento SHALL ser estornado no máximo uma vez, e um estorno não pode ser estornado. Um segundo estorno do mesmo lançamento recebe `409` com código `lancamento-ja-estornado`. O estorno de um estorno recebe `422` com código `estorno-nao-estornavel`.

#### Scenario: Segundo estorno com outra chave
- **WHEN** o titular estorna um lançamento já estornado, usando uma `Idempotency-Key` nova
- **THEN** a resposta é `409` com código `lancamento-ja-estornado`
- **AND** o saldo não muda

#### Scenario: Estorno de um estorno
- **WHEN** o titular tenta estornar um lançamento do tipo `Estorno`
- **THEN** a resposta é `422` com código `estorno-nao-estornavel`

### Requirement: Lançamento a estornar precisa existir na conta
Quando o `lancamentoId` não existe ou pertence a outra conta, o estorno SHALL ser recusado com `404` e código `lancamento-nao-encontrado`.

#### Scenario: Lançamento de outra conta
- **WHEN** o titular tenta estornar, pela conta `C1`, um lançamento da sua outra conta `C2`
- **THEN** a resposta é `404` com código `lancamento-nao-encontrado`

### Requirement: Lançamentos imutáveis
O sistema SHALL não oferecer nenhuma forma de alterar ou excluir um lançamento. A única correção possível é o estorno.

#### Scenario: Tentativa de excluir um lançamento
- **WHEN** um cliente envia `DELETE`, `PUT` ou `PATCH` para `/lancamentos/contas/{contaId}/lancamentos/{lancamentoId}`
- **THEN** a resposta é `404` ou `405`
- **AND** o lançamento e o saldo permanecem inalterados

### Requirement: Idempotência dos lançamentos
Créditos, débitos e estornos SHALL exigir o cabeçalho `Idempotency-Key`, com 1 a 100 caracteres visíveis e única por conta. A repetição com a mesma chave e o mesmo conteúdo devolve a resposta original sem criar outro lançamento. Chave ausente ou inválida recebe `400` (`chave-idempotencia-obrigatoria` ou `chave-idempotencia-invalida`).

#### Scenario: Repetição após timeout
- **WHEN** o cliente repete um débito com a mesma `Idempotency-Key`, o mesmo valor e a mesma descrição
- **THEN** a resposta tem o mesmo status e o mesmo `lancamentoId` da original
- **AND** o saldo foi debitado uma única vez

#### Scenario: Mesma chave com outro conteúdo
- **WHEN** o cliente reutiliza a `Idempotency-Key` de um débito de `30.00` para enviar um débito de `50.00` na mesma conta
- **THEN** a resposta é `409` com código `chave-idempotencia-conflitante`
- **AND** nada muda

#### Scenario: Repetição de uma operação recusada
- **WHEN** um débito foi recusado por saldo insuficiente e o cliente o repete com a mesma chave, depois de um crédito
- **THEN** o débito é avaliado de novo, porque operações recusadas não ficam registradas

### Requirement: Operações simultâneas na mesma conta
Operações simultâneas sobre a mesma conta SHALL produzir o mesmo resultado de alguma execução em série. O saldo nunca ultrapassa o limite, nenhuma operação aceita se perde e a sequência continua sem lacunas.

#### Scenario: Débitos concorrentes
- **WHEN** uma conta tem saldo `50.00` e limite `0.00` e recebe 10 débitos simultâneos de `10.00`, cada um com chave própria
- **THEN** exatamente 5 débitos recebem `201` e os demais recebem `422` com `saldo-insuficiente`
- **AND** o saldo final é `0.00` e as sequências aceitas são contíguas

### Requirement: Formato dos erros
Toda resposta de erro da Lancamentos.Api SHALL usar `application/problem+json` (Problem Details), com o campo `codigo` estável e legível por máquina. Ela não pode conter stack trace, SQL, nomes internos de tipos nem dados de outros clientes. Um corpo JSON malformado ou com tipos errados recebe `400` com código `requisicao-invalida`.

#### Scenario: JSON malformado
- **WHEN** o corpo do crédito não é um JSON válido
- **THEN** a resposta é `400` em `application/problem+json` com código `requisicao-invalida`

#### Scenario: Falha inesperada
- **WHEN** ocorre um erro técnico não previsto durante a operação
- **THEN** a resposta é `500` em `application/problem+json`, sem stack trace nem mensagem da exceção
