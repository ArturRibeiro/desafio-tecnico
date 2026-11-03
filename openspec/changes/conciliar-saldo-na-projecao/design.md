# Design

## Context

A motivação está em [proposal.md](proposal.md) e o comportamento exigido nos deltas de `eventos-de-integracao` e `projecao-da-posicao`.

Estado atual:

- **Lançamentos** já grava `SaldoDisponivelApos` em cada `Lancamento` (`Conta` passa o `SaldoDisponivel` depois de aplicar o efeito). `SaldoDisponivel` é o saldo puro: o `Limite` fica separado, então é comparável ao saldo da Posição.
- **Contrato:** `MovimentacaoRegistradaV1` (`Contratos.Integracao/Lancamentos`) não traz saldo. O `TradutorDeEventos` de Lançamentos monta o contrato a partir do `Lancamento`. O teste `ContratosTestes` compara os campos serializados com o JSON da seção 7 do DOMINIO.md.
- **Posição:** o `TradutorDeEventos` da Infrastructure cria o `Movimento`. O Domain Service `ProjecaoDaConta.Aplicar` aplica os pendentes em ordem de sequência; depois de `atual.Aplicar(movimento)`, `atual.SaldoFechamento` é o saldo da conta após aquela sequência. `Reconstruir` reusa `Aplicar` a partir do zero.
- **Quem chama a projeção:** `AplicacaoDePendentes` (consumo de `MovimentacaoRegistrada` e de `ContaAberta`) e `ReconstruirPosicaoHandler`. A Application não usa `ILogger`; os logs do consumo ficam no Worker (`Registros`) e os da Infrastructure usam `ILogger` diretamente.
- **Métricas:** não há pipeline de métricas; o OpenTelemetry só exporta traces.
- **Testes de cenário:** o `ServicoDoWorker` sobe o Worker com `WebApplicationFactory`, e `EventosSinteticos.Movimentacao` publica movimentações sintéticas.

## Goals / Non-Goals

**Goals:**

- Detectar, por sequência, toda diferença entre o saldo calculado pela Posição e o informado por Lançamentos, tanto no consumo quanto na reconstrução.
- Manter a versão 1 do contrato compatível nos dois sentidos.
- Não bloquear a projeção nem alterar o saldo calculado por causa de uma divergência.

**Non-Goals:**

- Corrigir a posição automaticamente.
- Exportar métricas para um sistema de monitoramento ou configurar alertas.
- Persistir as divergências numa tabela própria.

## Decisions

### 1. Campo opcional `saldoDisponivelApos` na v1

`MovimentacaoRegistradaV1` ganha `decimal? SaldoDisponivelApos`, preenchido pelo `TradutorDeEventos` de Lançamentos com `lancamento.SaldoDisponivelApos`. O nome segue a Ubiquitous Language ("saldo disponível") e o nome já usado no Domínio.

- **Por que opcional na v1, e não uma v2:** pela regra do DOMINIO (seção 7), campo novo opcional é mudança compatível. Consumidores antigos ignoram o campo; o consumidor novo trata ausência como "sem comparação", o que cobre avisos já parados no Outbox ou nas filas no momento do deploy.
- **Alternativa descartada:** um evento separado e periódico de "saldo conferido". Exigiria agendamento em Lançamentos e só detectaria a divergência depois; o campo no próprio aviso concilia cada movimentação sem código novo de publicação.
- **Exposição de dado:** o saldo passa a trafegar no evento. Não é dado novo para a Posição, que já calcula o mesmo saldo, e continua sem identificar o titular.

### 2. O `Movimento` guarda o saldo informado

`Movimento` ganha `decimal? SaldoInformadoApos`, recebido em `Movimento.Criar` e persistido na coluna anulável `saldo_informado_apos` (`numeric(14,2)`), com uma migração na Posicao.Infrastructure. Movimentos já gravados ficam com `null`.

- **Por que persistir:** a comparação acontece quando o movimento é aplicado, que pode ser bem depois de recebido (lacuna na sequência). E a reconstrução só consegue conciliar se o valor estiver gravado.

### 3. A comparação fica no Domain Service

Em `ProjecaoDaConta.Aplicar`, logo depois de `atual.Aplicar(movimento)`: se `movimento.SaldoInformadoApos` tem valor e é diferente de `atual.SaldoFechamento`, o resultado registra uma `Divergencia(ContaId, Sequencia, LancamentoId)`. `ResultadoDaProjecao` ganha `IReadOnlyList<Divergencia> Divergencias`, devolvida também nos retornos antecipados (interrupções).

- **Por que no Domínio:** é regra de negócio sobre o saldo, e assim `Aplicar` e `Reconstruir` conciliam pelo mesmo código, sem duplicação.
- **Comparação:** igualdade de `decimal` (`70.00m == 70m`), sem tolerância: os dois lados usam duas casas e as mesmas somas.
- A divergência não muda o saldo nem interrompe a aplicação: o saldo informado é usado só para conferir.

### 4. Registro por uma porta da Application

Nova porta `IRegistroDeDivergencias` em `Posicao.Application/Portas`, com `Registrar(Divergencia)`. `AplicacaoDePendentes` e `ReconstruirPosicaoHandler` chamam a porta para cada divergência do resultado. A implementação `RegistroDeDivergenciasEmLog`, na Infrastructure, escreve um log `Error` com o código `saldo-divergente`, a conta, a sequência e o lançamento, sem valores (regra da spec `observabilidade`), e incrementa o contador `posicao.conciliacao.divergencias` de um `Meter` (`System.Diagnostics.Metrics`, sem pacote novo).

- **Por que uma porta, e não o log no Worker:** a reconstrução é chamada pelo `Program` do Worker e pelo teste de cenário direto pelo Mediator. Com a porta, os dois caminhos registram do mesmo jeito, e a Application continua sem `ILogger`.
- **Métrica sem exportação:** o contador já fica disponível para `dotnet-counters` e para quem ligar `WithMetrics` no futuro. Ligar a exportação ficaria fora do escopo: o Jaeger local só recebe traces.
- **Registro dentro da transação:** se a transação falhar e o evento for retentado, a mesma divergência é registrada de novo. Repetir um alerta é aceitável; perder um não.

### 5. Testes

- **Unidade (`Posicao.Unit.Tests`):** `ProjecaoDaConta` com saldo igual, divergente, ausente, divergência em movimento destravado por lacuna e na reconstrução.
- **Contrato (`Lancamentos.Unit.Tests`):** `ContratosTestes` preenche `SaldoDisponivelApos`, e o JSON da seção 7 do DOMINIO passa a ter o campo.
- **Cenários (`Scenarios.Integration.Specs.Tests`):**
  - `EventosSinteticos.Movimentacao` ganha o parâmetro opcional `saldoDisponivelApos`.
  - O `ServicoDoWorker` registra, em `ConfigureTestServices`, um `ILoggerProvider` que captura os logs com o código `saldo-divergente`, para os passos verificarem "divergência registrada" e "nenhuma divergência".
  - O cenário "Débito aceito" de `EventosDeIntegracao` passa a conferir o saldo de R$ 70,00 no aviso.

## Risks / Trade-offs

- **Falso positivo por defeito em Lançamentos:** uma divergência não diz qual lado está errado. → O log identifica conta e sequência; a análise compara com o histórico imutável de lançamentos.
- **Divergência só é vista no log:** sem exportação de métricas, depende de alguém olhar os logs. → O log é `Error` com código estável, pronto para uma regra de alerta; a exportação fica como evolução documentada.
- **Divergência repetida:** depois de uma divergência, as sequências seguintes também divergem (o erro se propaga no saldo), gerando um log por movimentação até a correção. → Aceitável e útil: mostra o alcance; a primeira sequência indica a origem.
