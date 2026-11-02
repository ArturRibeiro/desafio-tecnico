# Tasks

## 1. Fundação

- [x] 1.1 Criar `tests/Scenarios.Integration.Specs.Tests` com `SpecFlow.xUnit`, passo sem binding como erro (D1), execução em série e entrada no `DesafioTecnico.slnx`. Ajustar a convenção de nomes (`openspec/config.yaml` e `ProjetosDeTeste_SeguemONomePorTipo`). Verificação: um `.feature` de exemplo com passo sem binding falha e, com o binding, passa.
- [x] 1.2 Escrever os `.feature` das 6 specs de negócio (D2, D3) e a regra de tarefas no `openspec/config.yaml`. Verificação: os 6 `.feature` somam 69 cenários com o texto das specs.

## 2. Ambiente de execução

- [x] 2.1 Containers compartilhados (PostgreSQL, RabbitMQ com porta fixa, Redis) no `[BeforeTestRun]`, bancos novos por cenário e `ContextoDoCenario` com injeção de contexto (D5, D6). Verificação: um cenário cria os dois bancos e consegue consultá-los; os containers são descartados no fim.
- [x] 2.2 `Hooks` que sobe Lancamentos.Api, Posicao.Api e Posicao.Worker no `[BeforeScenario]`, com configuração por cenário (bancos, prefixo de filas, autenticação, `FakeTimeProvider`), tokens do `EmissorDeTokens` e helpers de HTTP e de espera por condição. Verificação: um cenário de teste abre conta, credita e vê a posição atualizada pelo Worker.
- [x] 2.3 Injeção de falhas: broker parado e restaurado no `[AfterScenario]`, banco indisponível por um instante, Redis fora e Worker parado e retomado. Verificação: um cenário de teste que falha com o broker parado termina com o broker de volta.

## 3. Passos por spec de negócio

Cada tarefa termina com todos os cenários da spec passando.

- [x] 3.1 `contas` (6) e `titularidade` (6).
- [x] 3.2 `lancamentos`, parte 1 (12): registrar crédito e débito, valor, descrição, conta ativa, sequência sem lacunas e formato dos erros.
- [x] 3.3 `lancamentos`, parte 2 (12): idempotência, data de competência, estorno (registrar, único, lançamento existente), lançamentos imutáveis e operações simultâneas.
- [x] 3.4 `eventos-de-integracao` (10), incluindo publicação com o broker fora e falha ao confirmar a transação.
- [x] 3.5 `projecao-da-posicao` (11), incluindo eventos repetidos, fora de ordem e inválidos, banco indisponível, Worker parado e reconstrução.
- [x] 3.6 `consulta-de-posicao` (12), incluindo Redis fora.

## 4. Integração

- [ ] 4.1 CI: conferir a suíte e o tempo dela no runner do GitHub Actions. Verificação: execução do workflow verde.
- [x] 4.2 Documentação: README (tabela de testes, como rodar e como manter os cenários, Docker como pré-requisito dos testes) e ARQUITETURA.md (os itens de negócio "Implementado, sem teste automatizado" passam a apontar para o cenário que os verifica). Verificação: links resolvem.
- [x] 4.3 Verificação final: build sem warnings, suíte inteira passando duas vezes seguidas, com os 69 cenários de negócio executados.
