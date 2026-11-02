# Spec Delta

## Purpose

Responde ao cliente a posição consolidada da própria conta em qualquer instante e os fechamentos diários de um período, deixando explícito até onde os dados estão atualizados.

## ADDED Requirements

### Requirement: Posição em um instante
`GET /posicao/contas/{contaId}?instante=` DEVE responder `200` com o saldo da conta no instante informado. Se o instante não for informado, vale o momento da consulta. O saldo é a soma do efeito das movimentações aplicadas cuja data de competência não passa do instante.

#### Scenario: Instante dentro de um dia
- **GIVEN** a conta `C` com crédito de `100.00` às `10:00` e débito de `30.00` às `15:00` de `2026-10-27`
- **WHEN** o titular consulta a posição em `2026-10-27T12:00:00Z`
- **THEN** o saldo é `100.00`

#### Scenario: Sem instante
- **GIVEN** a conta `C` com saldo aplicado de `70.00`
- **WHEN** o titular consulta a posição sem informar o instante
- **THEN** o saldo é `70.00`

#### Scenario: Instante anterior à primeira movimentação
- **WHEN** o titular consulta a posição em um instante anterior a qualquer movimentação da conta
- **THEN** o saldo é `0.00`

### Requirement: Instante válido
O instante informado DEVE ter fuso horário e não pode estar no futuro. Fora disso, a resposta é `400` com o código `instante-invalido`.

#### Scenario: Instante sem fuso
- **WHEN** o titular consulta a posição em `2026-10-27T12:00:00`
- **THEN** a resposta é `400` com o código `instante-invalido`

#### Scenario: Instante no futuro
- **WHEN** o titular consulta a posição em um instante posterior ao momento da consulta
- **THEN** a resposta é `400` com o código `instante-invalido`

### Requirement: Indicação de atualização
Toda resposta de consulta DEVE informar a sequência e o instante de registro da última movimentação aplicada à conta (`atualizadoAte`). Antes da primeira movimentação, o instante é o da abertura da conta, e a sequência é `0`.

#### Scenario: Movimentação pendente
- **GIVEN** a conta `C` com as sequências `1` e `2` aplicadas e a `4` pendente
- **WHEN** o titular consulta a posição
- **THEN** o saldo considera só as sequências `1` e `2`
- **AND** `atualizadoAte` informa a sequência `2` e o instante de registro dela

### Requirement: Fechamentos diários de um período
`GET /posicao/contas/{contaId}/fechamentos?de=&ate=` DEVE listar, em ordem crescente de data, os fechamentos dos dias do período (inclusive) que tiveram movimentação. A lista é paginada, com até 100 itens por página, e informa se há uma próxima página.

#### Scenario: Período com dias sem movimentação
- **GIVEN** a conta `C` com movimentações em `2026-10-27` e `2026-10-29`
- **WHEN** o titular consulta os fechamentos de `2026-10-27` a `2026-10-31`
- **THEN** a resposta traz só os fechamentos de `2026-10-27` e `2026-10-29`, nessa ordem

#### Scenario: Mais de uma página
- **GIVEN** a conta `C` com fechamentos em 3 dias do período
- **WHEN** o titular pede páginas de 2 itens
- **THEN** a primeira página traz os 2 primeiros fechamentos e indica que há uma próxima página
- **AND** a segunda página traz o terceiro fechamento e indica que não há outra

### Requirement: Período válido
As datas `de` e `ate` DEVEM ser informadas, com `de` menor ou igual a `ate` e no máximo 366 dias entre elas. O tamanho da página fica entre 1 e 100. Fora disso, a resposta é `400` com o código `periodo-invalido`.

#### Scenario: Período invertido
- **WHEN** o titular consulta os fechamentos com `de` posterior a `ate`
- **THEN** a resposta é `400` com o código `periodo-invalido`

#### Scenario: Período longo demais
- **WHEN** o titular consulta os fechamentos de um intervalo de 400 dias
- **THEN** a resposta é `400` com o código `periodo-invalido`

### Requirement: Cache sem efeito no resultado
O cache DEVE apenas acelerar as consultas: com ou sem ele, a resposta é a mesma. A falta do cache não causa erro, e uma movimentação nova aparece na consulta seguinte à sua aplicação.

#### Scenario: Cache fora do ar
- **GIVEN** que o cache está inacessível
- **WHEN** o titular consulta a posição
- **THEN** a resposta é `200` com o mesmo saldo de quando o cache está disponível

#### Scenario: Movimentação depois de uma consulta
- **GIVEN** uma consulta à posição da conta `C` já respondida
- **WHEN** uma nova movimentação de `C` é aplicada e o titular consulta de novo
- **THEN** o saldo inclui a nova movimentação
