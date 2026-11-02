# Cenários de openspec/specs/contas/spec.md. Uma change que altera os cenários da spec atualiza este arquivo.
Feature: contas
  Abertura de contas no contexto de Lançamentos. A conta é onde os lançamentos acontecem e guarda o titular, a situação, o limite e o saldo disponível usados para autorizar débitos.

  Rule: Abrir conta
    Um cliente autenticado DEVE poder abrir uma conta com `POST /lancamentos/contas`. A conta nasce `Ativa`, em `BRL`, com saldo disponível zero, limite zero e o cliente autenticado como titular. A resposta é `201` com o identificador da conta, a situação, a moeda, o saldo disponível e o limite, e com o cabeçalho `Location`.

    Scenario: Abertura bem-sucedida
      When um cliente abre uma conta
      Then a conta é aberta ativa, em reais, com saldo de R$ 0,00 e sem limite
      And o cliente é o titular da conta

    Scenario: Limite informado pelo cliente
      When um cliente abre uma conta pedindo um limite de R$ 1.000,00
      Then a conta é aberta sem limite

  Rule: Identificador de conta não sequencial
    O identificador da conta DEVE ser gerado pelo sistema, aleatório e não sequencial, para não expor volume nem permitir adivinhar contas de outros clientes.

    Scenario: Duas aberturas seguidas
      When um cliente abre duas contas, uma depois da outra
      Then o número de uma conta não permite deduzir o número da outra

  Rule: Abertura idempotente
    A abertura DEVE exigir o cabeçalho `Idempotency-Key`. Repetir a abertura com a mesma chave, pelo mesmo titular, devolve a mesma conta, com o mesmo status e o mesmo corpo da primeira resposta, sem criar outra conta. A chave é única por titular.

    Scenario: Repetição da abertura
      When um cliente repete o mesmo pedido de abertura de conta
      Then ele recebe a mesma conta da primeira vez
      And só uma conta é aberta

    Scenario: Mesma chave por titulares diferentes
      When os clientes A e B fazem pedidos de abertura com a mesma identificação de pedido
      Then cada um recebe a sua própria conta

    Scenario: Abertura sem chave
      When um cliente pede a abertura de uma conta sem identificar o pedido
      Then a abertura é recusada por falta da identificação do pedido
