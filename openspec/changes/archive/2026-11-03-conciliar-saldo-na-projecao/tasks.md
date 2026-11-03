# Tasks

## 1. Contrato e Lançamentos

- [x] 1.1 Acrescentar `decimal? SaldoDisponivelApos` a `MovimentacaoRegistradaV1` e preenchê-lo no `TradutorDeEventos` de Lançamentos com `lancamento.SaldoDisponivelApos` (D1). Atualizar o JSON de `MovimentacaoRegistrada v1` na seção 7 do DOMINIO.md e o `ContratosTestes`. Verificação: `Lancamentos.Unit.Tests` passa, com o campo documentado e serializado como texto decimal.

## 2. Domínio e persistência da Posição

- [x] 2.1 `Movimento.SaldoInformadoApos` (recebido em `Movimento.Criar`), record `Divergencia` e `ResultadoDaProjecao.Divergencias`; comparação em `ProjecaoDaConta.Aplicar` logo depois de aplicar cada movimento, também nos retornos antecipados (D2, D3). Verificação: testes de unidade em `Posicao.Unit.Tests` para saldo igual, divergente, ausente, divergência em movimento destravado por lacuna e na reconstrução.
- [x] 2.2 Mapear `saldo_informado_apos` (`numeric(14,2)`, anulável) no `PosicaoDbContext`, gerar a migração e passar o campo do contrato ao `Movimento` no `TradutorDeEventos` da Posição. Verificação: o build passa e a migração cria só a coluna nova; teste de unidade do tradutor com e sem o campo.

## 3. Registro das divergências

- [x] 3.1 Porta `IRegistroDeDivergencias` na Application, chamada por `AplicacaoDePendentes` e por `ReconstruirPosicaoHandler` para cada divergência; implementação `RegistroDeDivergenciasEmLog` na Infrastructure (log `Error` com `saldo-divergente`, conta, sequência e lançamento, sem valores, e contador `posicao.conciliacao.divergencias`) registrada em `ServicosDeInfraestrutura` (D4). Verificação: testes de unidade dos dois casos de uso com uma porta falsa; `Architecture.Tests` passa.

## 4. Cenários de negócio

- [x] 4.1 Atualizar o `.feature` de `eventos-de-integracao` (texto da `Rule` "Evento de movimentação registrada" e o `Then` do cenário "Débito aceito") e o passo em `EventosDeIntegracaoStep` para conferir o saldo de R$ 70,00 no aviso. Verificação: os 10 cenários de `EventosDeIntegracao.feature` passam.
- [x] 4.2 Acrescentar ao `ProjecaoDaPosicao.feature` a `Rule` "Conciliação com o saldo de Lançamentos", com os 4 cenários da spec; parâmetro opcional de saldo em `EventosSinteticos.Movimentacao`; captura dos logs `saldo-divergente` no `ServicoDoWorker`; passos em `ProjecaoDaPosicaoStep` (D5). Verificação: os 15 cenários de `ProjecaoDaPosicao.feature` passam.

## 5. Documentação e verificação

- [x] 5.1 Documentação: DOMINIO.md (nova subseção de conciliação na seção 6, com a regra e o registro da divergência), ARQUITETURA.md 3.1 (a linha de conciliação passa a "Implementado", apontando para o cenário `Saldo divergente`), DESENVOLVIMENTO.md (eventos e contagem de cenários) e item 2 do DIVERGENCIAS.md marcado. Verificação: os links resolvem.
- [x] 5.2 Verificação final: build sem warnings e `dotnet test -c Release` com todos os projetos passando, inclusive os 73 cenários de negócio.

## Workflow follow-up

- Sincronizar os deltas com as specs principais e arquivar a change (`/opsx:archive`).
