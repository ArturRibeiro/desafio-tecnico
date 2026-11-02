# Proposal

## Why

- Os cenários das specs de negócio viram os `.feature` executados pelo `Scenarios.Integration.Specs.Tests`, mas boa parte dos passos descreve a API e a infraestrutura, e não o negócio.
- Isso inclui rotas, status, campos, cabeçalhos, broker, cache e Worker.
- Um cenário assim só é entendido por quem conhece a implementação, e muda quando a API muda, mesmo sem mudança de regra.

## What Changes

- Os 69 cenários das 6 specs de negócio são reescritos em linguagem de negócio:
  - quem faz o quê;
  - com que valores, em reais e datas no formato brasileiro;
  - qual o resultado para o cliente: aceito, recusado e por quê, e saldo.
- O texto dos requisitos não muda. Ele continua sendo o contrato da API (rotas, status e códigos de erro).
- Os `.feature` continuam gerados das specs. Os passos do `Scenarios.Integration.Specs.Tests` passam a traduzir cada termo de negócio para os endpoints, com o mesmo ambiente (Testcontainers e `WebApplicationFactory`).
- Uma convenção de linguagem dos cenários de negócio entra nas convenções do projeto.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

Só os cenários mudam de redação, e o comportamento exigido continua o mesmo.

- `contas`
- `titularidade`
- `lancamentos`
- `eventos-de-integracao`
- `projecao-da-posicao`
- `consulta-de-posicao`

## Fora do escopo

- Mudar regras, respostas da API ou o comportamento dos serviços.
- Reescrever o texto dos requisitos.
- Specs técnicas (`autenticacao`, `api-gateway`, `saude-dos-servicos`, `observabilidade`, `ambiente-local` e `qualidade-de-build`).
- Adicionar, remover ou renomear cenários. Continuam 69, com os mesmos nomes.

## Impact

- **Specs:** os cenários das 6 specs de negócio e os `.feature` gerados delas.
- **Testes:** as classes de passos e as operações do `Scenarios.Integration.Specs.Tests`.
- **Documentação:**
  - as convenções do `openspec/config.yaml`.
