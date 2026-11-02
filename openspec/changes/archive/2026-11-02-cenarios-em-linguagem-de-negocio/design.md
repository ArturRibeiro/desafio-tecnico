# Design

## Context

A motivação está em [proposal.md](proposal.md).

- **Geração (na época desta change):** os `.feature` do `Scenarios.Integration.Specs.Tests` são gerados das specs principais (`openspec/specs`) pelo `GeradorDeCenarios`. O teste `Features_IguaisAsSpecs` falha se eles divergirem.
- **Formato:** cada requisito vira uma `Rule`, com o texto do requisito como descrição. Cada cenário vira um `Scenario` com os passos sem alteração.
- **Passos hoje:** são regex ancoradas sobre o texto técnico. Por exemplo, o passo comum "a resposta é `N` com código `x`" confere o status e o `codigo` do Problem Details.
- **Ciclo:** o `Hooks` sobe a infraestrutura e os serviços antes de cada cenário. Isso não muda.

## Goals / Non-Goals

**Goals:**

- Passos sem rotas, status, campos, cabeçalhos ou nomes de infraestrutura.
- Cada cenário verifica o mesmo comportamento de antes, com a mesma precisão: o motivo de uma recusa continua sendo conferido pelo status e pelo código de erro.
- Um vocabulário único, usado da mesma forma em todas as specs de negócio.

**Non-Goals:**

- Palavras-chave do Gherkin em português (`Dado/Quando/Então`): o gerador continua emitindo `Given/When/Then`.
- Mudar o texto dos requisitos ou o formato do gerador.

## Decisions

### D1. Vocabulário dos cenários

Uma tabela única liga o termo de negócio ao que o passo faz ou confere. Ela fica no design e nos passos.

| Termo no cenário | Tradução nos passos |
|---|---|
| R$ 1.000,00 | valor decimal em pt-BR |
| 27/10/2026, às 10h | data e hora em UTC |
| "é registrado" / "é aberta" | `201` |
| "é recusado por saldo insuficiente" | `422` `saldo-insuficiente` |
| "porque a conta não está ativa" | `422` `conta-inativa` |
| "por valor inválido" / "por descrição inválida" | `400` `valor-invalido` / `descricao-invalida` |
| "por data de competência inválida" | `422` `data-competencia-invalida` |
| "porque o lançamento já foi estornado" | `409` `lancamento-ja-estornado` |
| "porque um estorno não pode ser estornado" | `422` `estorno-nao-estornavel` |
| "porque o lançamento não foi encontrado" | `404` `lancamento-nao-encontrado` |
| "porque a conta não foi encontrada" | `404` `conta-nao-encontrada` |
| "por conflito com o pedido original" | `409` `chave-idempotencia-conflitante` |
| "por falta da identificação do pedido" | `400` `chave-idempotencia-obrigatoria` |
| "como inválido" / "por erro interno" | `400` `requisicao-invalida` / `500` |
| "por instante inválido" / "por período inválido" | `400` `instante-invalido` / `periodo-invalido` |
| "pedido repetido" / "identificação do pedido" | mesma `Idempotency-Key` |
| "não oferece essa possibilidade" | `404` ou `405` para `DELETE`, `PUT` e `PATCH` |
| "Lançamentos avisa…" | evento publicado (sonda no RabbitMQ) |
| "a Posição Consolidada toma conhecimento" / "aplica" | evento processado pelo Worker |
| "a entrega de avisos está indisponível" | RabbitMQ parado |
| "a Posição Consolidada está parada" | Worker parado |
| "a Posição Consolidada fica indisponível por um instante" | banco de Posição recusando conexões |
| "separada para análise" | fila `_error` |
| "a aceleração das consultas está indisponível" | Redis inacessível |
| "a posição é reconstruída" | comando `reconstruir` do Worker |
| "primeiro, segundo… lançamento" | `sequencia` 1, 2… |

*Alternativa descartada:* manter os códigos de erro nos passos ("recusado com `saldo-insuficiente`"). Ficaria mais curto, mas continua sendo vocabulário da API.

### D2. Só os cenários mudam

Os deltas são `MODIFIED`, mas o texto de cada requisito fica idêntico ao atual. O contrato da API continua documentado no requisito e aparece como descrição da `Rule` no `.feature`. Os nomes dos requisitos e dos cenários não mudam: o OpenSpec só aceita um requisito `MODIFIED` que mantém os cenários existentes pelo nome. Alguns nomes continuam citando tecnologia (por exemplo, "Broker fora durante as operações" e "Cache fora do ar"), mas os passos, que são o que se executa, ficam em linguagem de negócio.

### D3. Passos

- **Recusas:** um passo comum interpreta "é recusad[oa] (por|porque|como) …" e consulta a tabela de D1, com o status e o código esperados. Um motivo fora da tabela falha com o texto do motivo.
- **Valores e datas:** são convertidos com `pt-BR` explícito. O formato da spec é o único aceito.
- **Valores repetidos:** expressões só para valores que se repetem; os demais passos usam o texto literal, como hoje.
- **Execução:** as verificações continuam as mesmas (endpoints, sonda de eventos, banco quando a API não expõe o dado).

### D4. Specs principais atualizadas no apply

Os `.feature` são gerados das specs principais. Por isso, o primeiro passo do apply sincroniza os deltas com `openspec/specs` (`/opsx:sync`) e regenera os `.feature`. O arquivamento depois só move a change.

### D5. Convenção registrada

O `openspec/config.yaml` ganha a regra: cenários de specs de negócio usam linguagem de negócio (reais, datas no formato brasileiro, motivo da recusa em palavras), e o contrato técnico fica no texto do requisito.

## Risks / Trade-offs

- **[Ambiguidade]** "Recusado" sem status pode esconder uma troca de código. → Mitigação: a tabela de D1 fixa o status e o código de cada motivo, e o passo confere os dois.
- **[Rule com texto técnico]** A descrição da `Rule` no `.feature` continua citando rotas e códigos. → Mitigação: é o contrato da API, que precisa ficar na spec. Os cenários, que são o que se lê e executa, ficam em linguagem de negócio.
- **[Volume]** São 69 cenários e cerca de 180 passos reescritos. → Mitigação: tarefas por spec, com o passo comum de recusa e os conversores feitos primeiro.
- **[Nomes técnicos]** Alguns nomes de cenário continuam técnicos (D2). → Mitigação: renomeá-los fica para uma change que troque o cenário inteiro (remoção e inclusão), se for necessário.
