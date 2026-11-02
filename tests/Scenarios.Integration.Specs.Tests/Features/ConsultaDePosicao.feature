# Cenários de openspec/specs/consulta-de-posicao/spec.md. Uma change que altera os cenários da spec atualiza este arquivo.
Feature: consulta-de-posicao
  Responde ao cliente a posição consolidada da própria conta em qualquer instante e os fechamentos diários de um período, deixando explícito até onde os dados estão atualizados.

  Rule: Posição em um instante
    `GET /posicao/contas/{contaId}?instante=` DEVE responder `200` com o saldo da conta no instante informado. Se o instante não for informado, vale o momento da consulta. O saldo é a soma do efeito das movimentações aplicadas cuja data de competência não passa do instante.

    Scenario: Instante dentro de um dia
      Given a conta C com crédito de R$ 100,00 às 10h e débito de R$ 30,00 às 15h de 27/10/2026
      When o titular consulta a posição de 27/10/2026 às 12h (UTC)
      Then o saldo é R$ 100,00

    Scenario: Sem instante
      Given a conta C com saldo aplicado de R$ 70,00
      When o titular consulta a posição sem informar o instante
      Then o saldo é R$ 70,00

    Scenario: Instante anterior à primeira movimentação
      When o titular consulta a posição de um instante anterior a qualquer movimentação da conta
      Then o saldo é R$ 0,00

  Rule: Instante válido
    O instante informado DEVE ter fuso horário e não pode estar no futuro. Fora disso, a resposta é `400` com o código `instante-invalido`.

    Scenario: Instante sem fuso
      When o titular consulta a posição de 27/10/2026 às 12h sem informar o fuso horário
      Then a consulta é recusada por instante inválido

    Scenario: Instante no futuro
      When o titular consulta a posição de um instante no futuro
      Then a consulta é recusada por instante inválido

  Rule: Indicação de atualização
    Toda resposta de consulta DEVE informar a sequência e o instante de registro da última movimentação aplicada à conta (`atualizadoAte`). Antes da primeira movimentação, o instante é o da abertura da conta, e a sequência é `0`.

    Scenario: Movimentação pendente
      Given a conta C com o primeiro e o segundo lançamento aplicados e o quarto pendente
      When o titular consulta a posição
      Then o saldo considera só o primeiro e o segundo lançamento
      And a consulta informa que a posição está atualizada até o segundo lançamento, com o instante em que ele foi registrado

  Rule: Fechamentos diários de um período
    `GET /posicao/contas/{contaId}/fechamentos?de=&ate=` DEVE listar, em ordem crescente de data, os fechamentos dos dias do período (inclusive) que tiveram movimentação. A lista é paginada, com até 100 itens por página, e informa se há uma próxima página.

    Scenario: Período com dias sem movimentação
      Given a conta C com movimentações em 27/10/2026 e 29/10/2026
      When o titular consulta os fechamentos de 27/10/2026 a 31/10/2026
      Then a consulta traz só os fechamentos de 27/10/2026 e 29/10/2026, nessa ordem

    Scenario: Mais de uma página
      Given a conta C com fechamentos em 3 dias do período
      When o titular consulta os fechamentos em páginas de 2 dias
      Then a primeira página traz os 2 primeiros fechamentos e indica que há uma próxima página
      And a segunda página traz o terceiro fechamento e indica que não há outra

  Rule: Período válido
    As datas `de` e `ate` DEVEM ser informadas, com `de` menor ou igual a `ate` e no máximo 366 dias entre elas. O tamanho da página fica entre 1 e 100. Fora disso, a resposta é `400` com o código `periodo-invalido`.

    Scenario: Período invertido
      When o titular consulta os fechamentos de 29/10/2026 a 27/10/2026
      Then a consulta é recusada por período inválido

    Scenario: Período longo demais
      When o titular consulta os fechamentos de um período de 400 dias
      Then a consulta é recusada por período inválido

  Rule: Cache sem efeito no resultado
    O cache DEVE apenas acelerar as consultas: com ou sem ele, a resposta é a mesma. A falta do cache não causa erro, e uma movimentação nova aparece na consulta seguinte à sua aplicação.

    Scenario: Cache fora do ar
      Given que a aceleração das consultas está indisponível
      When o titular consulta a posição
      Then ele recebe o mesmo saldo de quando a aceleração está disponível

    Scenario: Movimentação depois de uma consulta
      Given que o titular já consultou a posição da conta C
      When uma nova movimentação de C é aplicada e o titular consulta de novo
      Then o saldo inclui a nova movimentação
