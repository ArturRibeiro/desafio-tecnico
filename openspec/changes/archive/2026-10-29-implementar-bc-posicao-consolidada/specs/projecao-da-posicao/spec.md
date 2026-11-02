# Spec Delta

## Purpose

Mantém, a partir dos eventos publicados por Lançamentos, a titularidade e a linha do tempo de saldos de cada conta, com cada movimentação aplicada uma única vez e na ordem da conta.

## ADDED Requirements

### Requirement: Titularidade projetada da abertura
A Posição Consolidada DEVE registrar o titular de cada conta a partir do evento `ContaAberta`. Ela não consulta Lançamentos para saber o titular.

#### Scenario: Conta aberta
- **GIVEN** que Lançamentos publicou `ContaAberta` da conta `C` com o titular `T`
- **WHEN** o evento é processado
- **THEN** a conta `C` passa a pertencer a `T` na Posição Consolidada
- **AND** o saldo de `C` é zero

### Requirement: Movimentação aplicada uma única vez
Cada movimentação DEVE afetar a posição uma única vez, mesmo que o seu evento seja entregue mais de uma vez.

#### Scenario: Evento repetido
- **GIVEN** um crédito de `100.00` com sequência `1` já aplicado à conta `C`
- **WHEN** o mesmo evento é entregue de novo
- **THEN** o saldo de `C` continua `100.00`

### Requirement: Movimentações aplicadas na ordem da conta
As movimentações de uma conta DEVEM ser aplicadas em ordem crescente de sequência e sem lacunas. Uma movimentação que chega antes da anterior fica pendente e é aplicada, junto com as seguintes, assim que a lacuna é preenchida.

#### Scenario: Evento fora de ordem
- **GIVEN** a conta `C` com a sequência `1` aplicada
- **WHEN** chega a sequência `3` antes da `2`
- **THEN** a sequência `3` fica pendente e não altera a posição
- **AND** quando a sequência `2` chega, as sequências `2` e `3` são aplicadas nessa ordem

#### Scenario: Movimentação antes da abertura
- **GIVEN** que o `ContaAberta` da conta `C` ainda não foi processado
- **WHEN** chega a movimentação de sequência `1` de `C`
- **THEN** ela fica pendente
- **AND** é aplicada logo depois que o `ContaAberta` de `C` é processado

### Requirement: Fechamento diário
Para cada dia (UTC) com movimentação aplicada, a conta DEVE ter um fechamento com saldo de abertura, total de créditos, total de débitos e saldo de fechamento. O saldo de abertura é o saldo de fechamento do último dia anterior com movimentação (zero se não houver), e o de fechamento é abertura + créditos − débitos.

#### Scenario: Dois dias com movimentação
- **GIVEN** a conta `C` com crédito de `100.00` em `2026-10-27` e débito de `30.00` em `2026-10-29`
- **WHEN** os dois eventos são aplicados
- **THEN** o fechamento de `2026-10-27` tem abertura `0.00`, créditos `100.00`, débitos `0.00` e fechamento `100.00`
- **AND** o fechamento de `2026-10-29` tem abertura `100.00`, créditos `0.00`, débitos `30.00` e fechamento `70.00`

#### Scenario: Estorno conta pelo efeito
- **GIVEN** um débito de `30.00` aplicado à conta `C`
- **WHEN** é aplicado o estorno desse débito
- **THEN** o estorno soma `30.00` aos créditos do dia e o saldo volta ao valor anterior ao débito

### Requirement: Falhas de processamento isoladas
Um evento cujo processamento falha DEVE ser tentado de novo com intervalo crescente e, esgotadas as tentativas, ir para uma fila de falhas. Um evento inválido, que nunca seria processado, vai direto para essa fila. Em nenhum dos casos ele bloqueia os demais eventos.

#### Scenario: Banco indisponível por um instante
- **GIVEN** que o banco de Posição fica indisponível durante o processamento de um evento
- **WHEN** o banco volta antes de esgotadas as tentativas
- **THEN** o evento é aplicado uma única vez

#### Scenario: Evento inválido
- **WHEN** chega uma movimentação com valor menor ou igual a zero ou com tipo desconhecido
- **THEN** ela vai para a fila de falhas sem alterar a posição
- **AND** os eventos seguintes de outras contas continuam sendo processados

### Requirement: Retomada após indisponibilidade
Quando o broker ou a Posição Consolidada ficam fora do ar, os eventos publicados nesse intervalo DEVEM ser processados assim que o serviço volta, sem intervenção manual.

#### Scenario: Worker volta depois de parado
- **GIVEN** que Lançamentos registrou movimentações enquanto o consumo de eventos estava parado
- **WHEN** o consumo volta
- **THEN** todas essas movimentações são aplicadas e a posição fica igual à de Lançamentos

### Requirement: Reconstrução da posição
Um comando documentado DEVE recalcular todos os fechamentos diários a partir das movimentações já recebidas pela Posição Consolidada. O resultado é idêntico ao do processamento original, e repetir o comando não muda nada.

#### Scenario: Fechamentos corrompidos
- **GIVEN** fechamentos diários alterados indevidamente
- **WHEN** o comando de reconstrução é executado
- **THEN** os fechamentos voltam a corresponder às movimentações recebidas

#### Scenario: Reconstrução repetida
- **GIVEN** fechamentos já reconstruídos
- **WHEN** o comando é executado de novo
- **THEN** os fechamentos não mudam
