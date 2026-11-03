# Cenários de openspec/specs/eventos-de-integracao/spec.md. Uma change que altera os cenários da spec atualiza este arquivo.
Feature: eventos-de-integracao
  Define os eventos que Lançamentos publica para outros Bounded Contexts (Published Language) e as garantias de entrega deles. Tudo o que foi registrado chega ao broker, sem depender de ele estar disponível na hora da operação e sem expor o modelo interno nem dados cadastrais do cliente.

  Rule: Evento de conta aberta
    Toda abertura de conta DEVE publicar um único `ContaAberta` versão `1`, com `eventoId`, `tipoEvento`, `versao`, `contaId`, `titularId`, `sequencia = 0`, `moeda`, `dataAbertura` e `correlacaoId`.

    Scenario: Conta aberta
      When um cliente abre uma conta
      Then Lançamentos avisa uma única vez que a conta foi aberta, informando a conta, o titular e a sequência 0

    Scenario: Abertura repetida com a mesma chave
      When o cliente repete o mesmo pedido de abertura
      Then Lançamentos não avisa a abertura de novo

  Rule: Evento de movimentação registrada
    Todo crédito, débito ou estorno aceito DEVE publicar um único `MovimentacaoRegistrada` versão `1`, com:
    - `eventoId`, `tipoEvento`, `versao`, `contaId`, `lancamentoId`;
    - `sequencia`, `tipo`, `valor`, `moeda`;
    - `dataCompetencia`, `dataRegistro`, `lancamentoEstornadoId` e `correlacaoId`;
    - `saldoDisponivelApos`, o saldo disponível da conta logo após a movimentação. O campo é opcional no contrato, para manter a versão `1` compatível com avisos publicados antes dele.

    Scenario: Débito aceito
      Given uma conta com dois lançamentos e saldo de R$ 100,00
      When o titular debita R$ 30,00
      Then Lançamentos avisa a movimentação: débito de R$ 30,00, terceiro lançamento da conta, sem lançamento estornado e com saldo de R$ 70,00 após a movimentação

    Scenario: Estorno aceito
      Given um débito registrado
      When o titular estorna esse débito
      Then Lançamentos avisa a movimentação como estorno, indicando o débito estornado

    Scenario: Operação recusada
      When um débito é recusado por saldo insuficiente
      Then Lançamentos não avisa nenhuma movimentação

  Rule: Evento gravado junto com a operação
    O evento DEVE ser gravado na mesma transação que a operação que o originou. Não pode existir operação confirmada sem o seu evento, nem evento de uma operação que não foi confirmada.

    Scenario: Falha ao confirmar a transação
      When a gravação de um crédito falha antes de ser concluída
      Then o crédito não fica registrado
      And Lançamentos não avisa nenhuma movimentação

  Rule: Entrega pelo menos uma vez, tolerante ao broker
    Os eventos DEVEM ser entregues ao broker pelo menos uma vez. Se o broker estiver indisponível, a operação continua sendo aceita normalmente, e os eventos pendentes são entregues automaticamente quando ele volta, sem intervenção manual.

    Scenario: Broker fora durante as operações
      When a entrega de avisos entre os contextos está indisponível e o titular credita R$ 25,00
      Then o crédito é registrado normalmente
      And quando a entrega volta, Lançamentos avisa a movimentação desse crédito

    Scenario: Consumidor recebe o evento duas vezes
      When o mesmo aviso de movimentação é entregue mais de uma vez
      Then todas as entregas têm a mesma identificação, para que quem recebe possa descartar as repetições

  Rule: Contrato sem dados cadastrais e com titular mínimo
    Os eventos DEVEM carregar apenas identificadores opacos, valores e datas, sem nome, documento nem contato do cliente. O `titularId` aparece somente em `ContaAberta`, nunca em `MovimentacaoRegistrada`.

    Scenario: Conteúdo de uma movimentação
      When Lançamentos avisa uma movimentação
      Then o aviso não identifica o titular nem traz a descrição do lançamento

  Rule: Contrato versionado e independente do modelo interno
    Os eventos DEVEM ser definidos no projeto de contratos de integração, separado do Domínio. Uma mudança incompatível no contrato exige uma nova versão do evento, que convive com a anterior.

    Scenario: Refatoração do Domínio
      When o modelo interno de Lançamentos evolui
      Then os avisos continuam com as mesmas informações da versão 1
