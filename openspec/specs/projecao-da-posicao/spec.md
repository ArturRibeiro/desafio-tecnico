# projecao-da-posicao Specification

## Purpose

Mantém, a partir dos eventos publicados por Lançamentos, a titularidade e a linha do tempo de saldos de cada conta, com cada movimentação aplicada uma única vez e na ordem da conta.

## Requirements

### Requirement: Titularidade projetada da abertura
A Posição Consolidada DEVE registrar o titular de cada conta a partir do evento `ContaAberta`. Ela não consulta Lançamentos para saber o titular.

#### Scenario: Conta aberta
- **GIVEN** que Lançamentos avisou a abertura da conta C do titular T
- **WHEN** a Posição Consolidada processa o aviso
- **THEN** a conta C passa a pertencer a T na Posição Consolidada
- **AND** o saldo de C é R$ 0,00

### Requirement: Movimentação aplicada uma única vez
Cada movimentação DEVE afetar a posição uma única vez, mesmo que o seu evento seja entregue mais de uma vez.

#### Scenario: Evento repetido
- **GIVEN** um crédito de R$ 100,00, primeiro lançamento da conta C, já aplicado à posição
- **WHEN** o aviso desse crédito chega de novo
- **THEN** o saldo de C continua R$ 100,00

### Requirement: Movimentações aplicadas na ordem da conta
As movimentações de uma conta DEVEM ser aplicadas em ordem crescente de sequência e sem lacunas. Uma movimentação que chega antes da anterior fica pendente e é aplicada, junto com as seguintes, assim que a lacuna é preenchida.

#### Scenario: Evento fora de ordem
- **GIVEN** a conta C com o primeiro lançamento aplicado
- **WHEN** o terceiro lançamento chega antes do segundo
- **THEN** o terceiro lançamento fica pendente e não altera a posição
- **AND** quando o segundo lançamento chega, o segundo e o terceiro são aplicados nessa ordem

#### Scenario: Movimentação antes da abertura
- **GIVEN** que a Posição Consolidada ainda não tomou conhecimento da abertura da conta C
- **WHEN** chega o primeiro lançamento de C
- **THEN** ele fica pendente
- **AND** é aplicado logo depois que a abertura de C é processada

### Requirement: Fechamento diário
Para cada dia (UTC) com movimentação aplicada, a conta DEVE ter um fechamento com saldo de abertura, total de créditos, total de débitos e saldo de fechamento. O saldo de abertura é o saldo de fechamento do último dia anterior com movimentação (zero se não houver), e o de fechamento é abertura + créditos − débitos.

#### Scenario: Dois dias com movimentação
- **GIVEN** a conta C com crédito de R$ 100,00 em 27/10/2026 e débito de R$ 30,00 em 29/10/2026
- **WHEN** as duas movimentações são aplicadas
- **THEN** o fechamento de 27/10/2026 tem abertura de R$ 0,00, créditos de R$ 100,00, débitos de R$ 0,00 e fechamento de R$ 100,00
- **AND** o fechamento de 29/10/2026 tem abertura de R$ 100,00, créditos de R$ 0,00, débitos de R$ 30,00 e fechamento de R$ 70,00

#### Scenario: Estorno conta pelo efeito
- **GIVEN** um débito de R$ 30,00 aplicado à conta C
- **WHEN** o estorno desse débito é aplicado
- **THEN** o estorno soma R$ 30,00 aos créditos do dia e o saldo volta ao valor anterior ao débito

### Requirement: Falhas de processamento isoladas
Um evento cujo processamento falha DEVE ser tentado de novo com intervalo crescente e, esgotadas as tentativas, ir para uma fila de falhas. Um evento inválido, que nunca seria processado, vai direto para essa fila. Em nenhum dos casos ele bloqueia os demais eventos.

#### Scenario: Banco indisponível por um instante
- **GIVEN** que a Posição Consolidada fica indisponível por um instante enquanto processa um crédito de R$ 10,00
- **WHEN** ela volta antes de esgotadas as tentativas
- **THEN** o crédito é aplicado uma única vez

#### Scenario: Evento inválido
- **WHEN** chega uma movimentação de valor zero ou de um tipo desconhecido
- **THEN** ela é separada para análise, sem alterar a posição
- **AND** as movimentações seguintes de outras contas continuam sendo aplicadas

### Requirement: Retomada após indisponibilidade
Quando o broker ou a Posição Consolidada ficam fora do ar, os eventos publicados nesse intervalo DEVEM ser processados assim que o serviço volta, sem intervenção manual.

#### Scenario: Worker volta depois de parado
- **GIVEN** que o titular registrou movimentações enquanto a Posição Consolidada estava parada
- **WHEN** a Posição Consolidada volta
- **THEN** todas essas movimentações são aplicadas e a posição fica igual ao saldo em Lançamentos

### Requirement: Reconstrução da posição
Um comando documentado DEVE recalcular todos os fechamentos diários a partir das movimentações já recebidas pela Posição Consolidada. O resultado é idêntico ao do processamento original, e repetir o comando não muda nada.

#### Scenario: Fechamentos corrompidos
- **GIVEN** fechamentos diários alterados indevidamente
- **WHEN** a posição é reconstruída
- **THEN** os fechamentos voltam a corresponder às movimentações recebidas

#### Scenario: Reconstrução repetida
- **GIVEN** fechamentos já reconstruídos
- **WHEN** a posição é reconstruída de novo
- **THEN** os fechamentos não mudam
