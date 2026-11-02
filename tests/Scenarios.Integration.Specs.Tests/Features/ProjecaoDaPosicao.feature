# Cenários de openspec/specs/projecao-da-posicao/spec.md. Uma change que altera os cenários da spec atualiza este arquivo.
Feature: projecao-da-posicao
  Mantém, a partir dos eventos publicados por Lançamentos, a titularidade e a linha do tempo de saldos de cada conta, com cada movimentação aplicada uma única vez e na ordem da conta.

  Rule: Titularidade projetada da abertura
    A Posição Consolidada DEVE registrar o titular de cada conta a partir do evento `ContaAberta`. Ela não consulta Lançamentos para saber o titular.

    Scenario: Conta aberta
      Given que Lançamentos avisou a abertura da conta C do titular T
      When a Posição Consolidada processa o aviso
      Then a conta C passa a pertencer a T na Posição Consolidada
      And o saldo de C é R$ 0,00

  Rule: Movimentação aplicada uma única vez
    Cada movimentação DEVE afetar a posição uma única vez, mesmo que o seu evento seja entregue mais de uma vez.

    Scenario: Evento repetido
      Given um crédito de R$ 100,00, primeiro lançamento da conta C, já aplicado à posição
      When o aviso desse crédito chega de novo
      Then o saldo de C continua R$ 100,00

  Rule: Movimentações aplicadas na ordem da conta
    As movimentações de uma conta DEVEM ser aplicadas em ordem crescente de sequência e sem lacunas. Uma movimentação que chega antes da anterior fica pendente e é aplicada, junto com as seguintes, assim que a lacuna é preenchida.

    Scenario: Evento fora de ordem
      Given a conta C com o primeiro lançamento aplicado
      When o terceiro lançamento chega antes do segundo
      Then o terceiro lançamento fica pendente e não altera a posição
      And quando o segundo lançamento chega, o segundo e o terceiro são aplicados nessa ordem

    Scenario: Movimentação antes da abertura
      Given que a Posição Consolidada ainda não tomou conhecimento da abertura da conta C
      When chega o primeiro lançamento de C
      Then ele fica pendente
      And é aplicado logo depois que a abertura de C é processada

  Rule: Fechamento diário
    Para cada dia (UTC) com movimentação aplicada, a conta DEVE ter um fechamento com saldo de abertura, total de créditos, total de débitos e saldo de fechamento. O saldo de abertura é o saldo de fechamento do último dia anterior com movimentação (zero se não houver), e o de fechamento é abertura + créditos − débitos.

    Scenario: Dois dias com movimentação
      Given a conta C com crédito de R$ 100,00 em 27/10/2026 e débito de R$ 30,00 em 29/10/2026
      When as duas movimentações são aplicadas
      Then o fechamento de 27/10/2026 tem abertura de R$ 0,00, créditos de R$ 100,00, débitos de R$ 0,00 e fechamento de R$ 100,00
      And o fechamento de 29/10/2026 tem abertura de R$ 100,00, créditos de R$ 0,00, débitos de R$ 30,00 e fechamento de R$ 70,00

    Scenario: Estorno conta pelo efeito
      Given um débito de R$ 30,00 aplicado à conta C
      When o estorno desse débito é aplicado
      Then o estorno soma R$ 30,00 aos créditos do dia e o saldo volta ao valor anterior ao débito

  Rule: Falhas de processamento isoladas
    Um evento cujo processamento falha DEVE ser tentado de novo com intervalo crescente e, esgotadas as tentativas, ir para uma fila de falhas. Um evento inválido, que nunca seria processado, vai direto para essa fila. Em nenhum dos casos ele bloqueia os demais eventos.

    Scenario: Banco indisponível por um instante
      Given que a Posição Consolidada fica indisponível por um instante enquanto processa um crédito de R$ 10,00
      When ela volta antes de esgotadas as tentativas
      Then o crédito é aplicado uma única vez

    Scenario: Evento inválido
      When chega uma movimentação de valor zero ou de um tipo desconhecido
      Then ela é separada para análise, sem alterar a posição
      And as movimentações seguintes de outras contas continuam sendo aplicadas

  Rule: Retomada após indisponibilidade
    Quando o broker ou a Posição Consolidada ficam fora do ar, os eventos publicados nesse intervalo DEVEM ser processados assim que o serviço volta, sem intervenção manual.

    Scenario: Worker volta depois de parado
      Given que o titular registrou movimentações enquanto a Posição Consolidada estava parada
      When a Posição Consolidada volta
      Then todas essas movimentações são aplicadas e a posição fica igual ao saldo em Lançamentos

  Rule: Reconstrução da posição
    Um comando documentado DEVE recalcular todos os fechamentos diários a partir das movimentações já recebidas pela Posição Consolidada. O resultado é idêntico ao do processamento original, e repetir o comando não muda nada.

    Scenario: Fechamentos corrompidos
      Given fechamentos diários alterados indevidamente
      When a posição é reconstruída
      Then os fechamentos voltam a corresponder às movimentações recebidas

    Scenario: Reconstrução repetida
      Given fechamentos já reconstruídos
      When a posição é reconstruída de novo
      Then os fechamentos não mudam
