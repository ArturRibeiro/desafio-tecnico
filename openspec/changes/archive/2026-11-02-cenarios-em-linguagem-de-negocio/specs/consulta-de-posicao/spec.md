# Spec Delta

## MODIFIED Requirements

### Requirement: Posição em um instante
`GET /posicao/contas/{contaId}?instante=` DEVE responder `200` com o saldo da conta no instante informado. Se o instante não for informado, vale o momento da consulta. O saldo é a soma do efeito das movimentações aplicadas cuja data de competência não passa do instante.

#### Scenario: Instante dentro de um dia
- **GIVEN** a conta C com crédito de R$ 100,00 às 10h e débito de R$ 30,00 às 15h de 27/10/2026
- **WHEN** o titular consulta a posição de 27/10/2026 às 12h (UTC)
- **THEN** o saldo é R$ 100,00

#### Scenario: Sem instante
- **GIVEN** a conta C com saldo aplicado de R$ 70,00
- **WHEN** o titular consulta a posição sem informar o instante
- **THEN** o saldo é R$ 70,00

#### Scenario: Instante anterior à primeira movimentação
- **WHEN** o titular consulta a posição de um instante anterior a qualquer movimentação da conta
- **THEN** o saldo é R$ 0,00

### Requirement: Instante válido
O instante informado DEVE ter fuso horário e não pode estar no futuro. Fora disso, a resposta é `400` com o código `instante-invalido`.

#### Scenario: Instante sem fuso
- **WHEN** o titular consulta a posição de 27/10/2026 às 12h sem informar o fuso horário
- **THEN** a consulta é recusada por instante inválido

#### Scenario: Instante no futuro
- **WHEN** o titular consulta a posição de um instante no futuro
- **THEN** a consulta é recusada por instante inválido

### Requirement: Indicação de atualização
Toda resposta de consulta DEVE informar a sequência e o instante de registro da última movimentação aplicada à conta (`atualizadoAte`). Antes da primeira movimentação, o instante é o da abertura da conta, e a sequência é `0`.

#### Scenario: Movimentação pendente
- **GIVEN** a conta C com o primeiro e o segundo lançamento aplicados e o quarto pendente
- **WHEN** o titular consulta a posição
- **THEN** o saldo considera só o primeiro e o segundo lançamento
- **AND** a consulta informa que a posição está atualizada até o segundo lançamento, com o instante em que ele foi registrado

### Requirement: Fechamentos diários de um período
`GET /posicao/contas/{contaId}/fechamentos?de=&ate=` DEVE listar, em ordem crescente de data, os fechamentos dos dias do período (inclusive) que tiveram movimentação. A lista é paginada, com até 100 itens por página, e informa se há uma próxima página.

#### Scenario: Período com dias sem movimentação
- **GIVEN** a conta C com movimentações em 27/10/2026 e 29/10/2026
- **WHEN** o titular consulta os fechamentos de 27/10/2026 a 31/10/2026
- **THEN** a consulta traz só os fechamentos de 27/10/2026 e 29/10/2026, nessa ordem

#### Scenario: Mais de uma página
- **GIVEN** a conta C com fechamentos em 3 dias do período
- **WHEN** o titular consulta os fechamentos em páginas de 2 dias
- **THEN** a primeira página traz os 2 primeiros fechamentos e indica que há uma próxima página
- **AND** a segunda página traz o terceiro fechamento e indica que não há outra

### Requirement: Período válido
As datas `de` e `ate` DEVEM ser informadas, com `de` menor ou igual a `ate` e no máximo 366 dias entre elas. O tamanho da página fica entre 1 e 100. Fora disso, a resposta é `400` com o código `periodo-invalido`.

#### Scenario: Período invertido
- **WHEN** o titular consulta os fechamentos de 29/10/2026 a 27/10/2026
- **THEN** a consulta é recusada por período inválido

#### Scenario: Período longo demais
- **WHEN** o titular consulta os fechamentos de um período de 400 dias
- **THEN** a consulta é recusada por período inválido

### Requirement: Cache sem efeito no resultado
O cache DEVE apenas acelerar as consultas: com ou sem ele, a resposta é a mesma. A falta do cache não causa erro, e uma movimentação nova aparece na consulta seguinte à sua aplicação.

#### Scenario: Cache fora do ar
- **GIVEN** que a aceleração das consultas está indisponível
- **WHEN** o titular consulta a posição
- **THEN** ele recebe o mesmo saldo de quando a aceleração está disponível

#### Scenario: Movimentação depois de uma consulta
- **GIVEN** que o titular já consultou a posição da conta C
- **WHEN** uma nova movimentação de C é aplicada e o titular consulta de novo
- **THEN** o saldo inclui a nova movimentação
