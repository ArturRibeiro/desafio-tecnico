# Tasks

## 1. Preparação

- [x] 1.1 Argumento `--quantidade N` no `EmissorDeTokens`: N tokens para `<titular>-1..N`, um por linha, numa execução (D3); uso atualizado no comentário do `Program.cs`. Verificação: `docker compose --profile ferramentas run --rm -q emissor-de-tokens --titular carga --quantidade 3` imprime 3 tokens com `sub` `carga-1..3`, e a chamada sem `--quantidade` continua igual; build sem warnings.
- [x] 1.2 Serviço `k6` no perfil `carga` do `docker-compose.yml` (imagem com tag exata, alvo `http://gateway:8080`, volume `carga/`) e `carga/.trabalho/` no `.gitignore` (D1). Verificação: `docker compose config --quiet` passa e `docker compose up -d` não sobe o k6.

## 2. Cenários

- [x] 2.1 Módulo comum em `carga/` (tokens por VU, `Idempotency-Key` única, métricas de 5xx, 429 e 404, `handleSummary` gravando o JSON bruto em `carga/resultados/` e a linha da tabela em `carga/.trabalho/`) e `escrita.js` (D4, D5, D7). Verificação: o cenário roda contra o ambiente com zero 5xx e zero 429 e grava os dois arquivos.
- [x] 2.2 `disputa.js`: débitos concorrentes numa conta com saldo para metade deles, conferindo aceitos × recusados pelo saldo final. Verificação: a execução mostra só os débitos que cabem aceitos e nenhum 5xx.
- [x] 2.3 `leitura.js`: escrita e consultas de posição em paralelo, lendo só contas já projetadas. Verificação: a execução grava a latência das consultas separada da escrita, sem 5xx.
- [x] 2.4 `alcance.js`: crédito-sonda de R$ 0,01 por conta da amostra, consulta da posição até igualar (com tempo limite), tempo de alcance e contagem de divergências (D6). Verificação: com o worker no ar, a amostra bate sem divergências e o tempo é gravado.

## 3. Orquestração e evidência

- [x] 3.1 `carga/executar.sh`: emite os tokens, roda os cenários 1 a 3, para o `posicao-worker`, roda a escrita, religa o worker, roda o `alcance.js`, monta `carga/resultados/resultados.md` (cabeçalho, linhas, consistência, data, CPU, memória e versão do Docker) e sai com erro se algum cenário falhou (D2, D7). Verificação: uma execução completa gera a tabela com as 4 linhas de cenário e a de consistência.
- [x] 3.2 Calibrar os valores padrão de VUs e durações na primeira execução (Open Question do design) e fazer a execução final cujos resultados ficam versionados. Verificação: `carga/resultados/` com a tabela e os JSON da execução final.

## 4. Documentação

- [x] 4.1 ARQUITETURA 3.3: tabela de resultados com a data, o ambiente, o comando para reproduzir e o aviso sobre o alcance dos números; coluna "No desafio" como "Medido", com link; gargalo observado descrito. Seção 5.3 sem o item do teste de carga. Verificação: os links resolvem e os números são os de `carga/resultados/resultados.md`.
- [x] 4.2 DESENVOLVIMENTO.md: seção "Teste de carga" (pré-requisitos, comando, variáveis para mudar VUs e durações, uso no Windows via WSL, onde ficam os resultados) e o `--quantidade` do emissor; README com uma linha apontando para ela. Verificação: seguir a seção do zero executa o teste.
- [x] 4.3 Verificação final: build sem warnings, `dotnet test -c Release` passando e `openspec validate teste-de-carga` sem erros.

## Workflow follow-up

- Sincronizar o delta com as specs principais e arquivar a change (`/opsx:archive`).
