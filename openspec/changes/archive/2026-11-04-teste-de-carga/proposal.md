# Proposal

## Why

O enunciado parte de um sistema com lentidão e instabilidade em horários de pico, mas os três cenários de alta demanda da arquitetura estão só "no desenho", sem nenhuma medição. É o único atributo de qualidade sem evidência, e falta resposta com números para "como a solução se comporta sob alta demanda".

## What Changes

- Teste de carga reproduzível contra o ambiente local, executado sob demanda, fora da subida normal e do CI.
- Quatro cenários: escrita em muitas contas; débitos concorrentes numa mesma conta; leitura junto com escrita; consolidado parado durante a carga e religado.
- Verificação de consistência ao final: o saldo de Lançamentos igual à posição consolidada numa amostra de contas.
- Evidência gerada pela própria execução: uma tabela de resultados, os resumos brutos de cada cenário versionados e o ambiente da medição.
- Resultados publicados na arquitetura, nos cenários de alta demanda, que passam de "sem teste de carga" para "Medido"; o teste de carga sai do roadmap de evolução.
- Instruções de execução no guia de desenvolvimento.
- O emissor de tokens de desenvolvimento passa a emitir vários tokens numa chamada, um titular por usuário virtual, para a carga não esbarrar no limite por titular do gateway.

## Capabilities

### New Capabilities
- `teste-de-carga`: execução reproduzível do teste de carga, seus cenários, a verificação de consistência e o registro da evidência dos resultados.

### Modified Capabilities
<!-- Nenhuma: o comportamento dos serviços não muda. -->

## Impact

- Novos scripts de carga e um perfil próprio no compose; nenhuma imagem nova na subida normal.
- Ferramenta de emissão de tokens (somente desenvolvimento) ganha a emissão em lote.
- Documentação: arquitetura (alta demanda e roadmap) e guia de desenvolvimento.
- Código de produção: sem alteração.

## Fora do escopo

- Metas de SLA ou capacidade de produção: o enunciado não define números, e a medição num notebook serve de referência e para achar o gargalo.
- Rodar a carga no CI.
- Corrigir gargalos encontrados: ficam registrados e, se for o caso, viram outra change.
- Testes de resistência de longa duração (soak) e de pico extremo (stress até a quebra).
