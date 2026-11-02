## Why

Lançamentos já publica `ContaAberta v1` e `MovimentacaoRegistrada v1`, mas nada consome esses eventos. Por isso, o cliente ainda não consegue saber o saldo de uma conta em um instante, que é a segunda necessidade do desafio.

## What Changes

- O Posicao.Worker passa a consumir os dois eventos de Lançamentos, com:
  - descarte de repetições;
  - retentativas;
  - fila de mensagens com falha.
- A titularidade de cada conta é projetada a partir da abertura, sem chamar Lançamentos.
- Cada movimentação é aplicada uma única vez e na ordem da sequência da conta.
  - Uma movimentação que chega antes da anterior fica pendente até a lacuna ser preenchida.
- O fechamento diário de cada conta é mantido com:
  - saldo de abertura;
  - total de créditos;
  - total de débitos;
  - saldo de fechamento.
- A Posicao.Api passa a responder duas consultas, ambas protegidas por token e restritas à própria conta:
  - a posição em um instante;
  - os fechamentos diários de um período.
- Toda consulta informa até onde os dados estão atualizados.
- Os dias que já não podem mudar ficam em cache opcional. Se o cache estiver fora do ar, as respostas continuam corretas.
- Um comando recalcula os fechamentos a partir das movimentações já recebidas.
- O banco de Posição é preparado automaticamente no ambiente local.
- A documentação passa a cobrir as consultas (README) e o domínio de Posição (DOMINIO).

## Capabilities

### New Capabilities

- `projecao-da-posicao`: consumo dos eventos, idempotência, ordem por sequência, pendências, fechamentos diários, falhas e reconstrução.
- `consulta-de-posicao`: posição em um instante, fechamentos de um período, indicação de atualização e cache opcional.

### Modified Capabilities

- `autenticacao`: as consultas de Posição passam a exigir token com escopo de leitura.
- `titularidade`: as consultas também se limitam à própria conta, incluindo o caso da conta recém-aberta ainda não projetada.
- `saude-dos-servicos`: os serviços de Posição passam a verificar se o esquema do banco está atualizado.
- `ambiente-local`: o banco de Posição também é preparado sem passos manuais.

## Impact

- **Código:** os cinco projetos de Posição, hoje vazios ou só com o endpoint de saúde.
- **Testes:**
  - um projeto novo de testes unitários de Posição;
  - ampliação dos testes de integração de Posição.
- **Lançamentos:** nenhum impacto. O contrato dos eventos não muda.
- **Ambiente local:** credenciais de autenticação para a Posicao.Api.
- **Documentação:** README e DOMINIO, seção 6.

## Fora do escopo

- Reenvio de eventos a partir de Lançamentos para recuperar eventos perdidos. Fica documentado como evolução.
- Autenticação e rate limiting no gateway.
- Observabilidade (logs estruturados, traces, métricas e alertas).
- Lançamentos retroativos, múltiplas moedas e transferências entre contas.
