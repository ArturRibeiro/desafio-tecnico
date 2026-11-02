# Tasks

## 1. Specs e geração

- [x] 1.1 Sincronizar os 6 deltas com `openspec/specs` (D4) e registrar a convenção de linguagem dos cenários no `openspec/config.yaml` (D5). Verificação: `openspec validate --specs` sem erros; os passos das 6 specs não citam rotas, status, campos, cabeçalhos, RabbitMQ, Redis nem Worker.
- [x] 1.2 Regenerar os `.feature` com `dotnet run --project ferramentas/GeradorDeCenarios`. Verificação: `Features_IguaisAsSpecs` passa e os 6 `.feature` somam 69 cenários.

## 2. Base dos passos

- [x] 2.1 Conversores de valor (R$ em pt-BR), data e hora (UTC) e o passo comum de recusa com a tabela de motivos de D1, substituindo os passos comuns de status e código. Verificação: um motivo desconhecido falha com o texto do motivo; os cenários de `contas` passam.

## 3. Passos por spec

Cada tarefa termina com todos os cenários da spec passando.

- [x] 3.1 `contas` (6) e `titularidade` (6).
- [x] 3.2 `lancamentos`, parte 1 (12): crédito, débito, conta ativa, valor, descrição, competência e sequência.
- [x] 3.3 `lancamentos`, parte 2 (12): estorno, estorno único, lançamento de outra conta, imutabilidade, idempotência, concorrência e erros.
- [x] 3.4 `eventos-de-integracao` (10).
- [x] 3.5 `projecao-da-posicao` (11).
- [x] 3.6 `consulta-de-posicao` (12).

## 4. Verificação

- [x] 4.1 Verificação final: build sem warnings, suíte inteira passando duas vezes seguidas e relatório de rastreio com os 69 cenários de negócio.
