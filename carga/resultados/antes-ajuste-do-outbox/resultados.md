# Resultados do teste de carga

Gerado por `carga/executar.sh` em 03/11/2026 22:52 (execução 1793753536).

Ambiente: Intel(R) Core(TM) i5-6400 CPU @ 2.70GHz (4 núcleos), 15 GB, Docker 29.9.2; todos os containers e o k6 na mesma máquina.

| Cenário | Usuários virtuais | Duração | Vazão (req/s) | Latência p50 | Latência p95 | Erros (5xx) | Observação |
|---|---|---|---|---|---|---|---|
| Escrita em muitas contas | 20 | 50 s | 55.9 | 27 ms | 86 ms | 0 | 2824 créditos e débitos em 20 contas; 0 respostas 429 |
| Consistência ao final da escrita | 20 | 1319 s | — | 1319 s | 1319 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1319 s |
| Débitos concorrentes na mesma conta | 60 | 1 s | 83.7 | 536 ms | 659 ms | 0 | 30 aceitos e 30 recusados por saldo, de 60 simultâneos (esperados 30 aceitos); saldo final correto. Serializados de propósito |
| Leitura junto com escrita | 50 | 97 s | 73.8 | 15 ms | 71 ms | 0 | latência e vazão das consultas, com 40 VUs de leitura e 10 de escrita; escrita no mesmo período: p95 67 ms |
| Consistência ao final da leitura | 20 | 725 s | — | 724 s | 725 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 725 s |
| Escrita com o consolidado parado | 20 | 50 s | 56.1 | 29 ms | 85 ms | 0 | 2814 créditos e débitos em 20 contas; 0 respostas 429 |
| Alcance depois de religar o consolidado | 20 | 1322 s | — | 1322 s | 1322 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1322 s |

Os números vêm de um notebook com o ambiente local (docker compose) e servem de referência e para encontrar o gargalo, não como capacidade de produção. Os resumos brutos do k6 de cada cenário estão nesta pasta (`*.json`).
