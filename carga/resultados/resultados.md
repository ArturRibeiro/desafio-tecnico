# Resultados do teste de carga

Gerado por `carga/executar.sh` em 04/11/2026 07:22 (execução 1793787573).

Ambiente: Intel(R) Core(TM) i5-6400 CPU @ 2.70GHz (4 núcleos), 15 GB, Docker 29.9.2; todos os containers e o k6 na mesma máquina.

| Cenário | Usuários virtuais | Duração | Vazão (req/s) | Latência p50 | Latência p95 | Erros (5xx) | Observação |
|---|---|---|---|---|---|---|---|
| Escrita em muitas contas | 20 | 52 s | 56.6 | 13 ms | 85 ms | 0 | 2930 créditos e débitos em 20 contas; 0 respostas 429 |
| Consistência ao final da escrita | 20 | 2 s | — | 1894 ms | 1895 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1895 ms |
| Débitos concorrentes na mesma conta | 60 | 1 s | 74.5 | 667 ms | 761 ms | 0 | 30 aceitos e 30 recusados por saldo, de 60 simultâneos (esperados 30 aceitos); saldo final correto. Serializados de propósito |
| Leitura junto com escrita | 50 | 52 s | 142.8 | 11 ms | 48 ms | 0 | latência e vazão das consultas, com 40 VUs de leitura e 10 de escrita; escrita no mesmo período: p95 246 ms |
| Consistência ao final da leitura | 20 | 1 s | — | 1061 ms | 1062 ms | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 1063 ms |
| Escrita com o consolidado parado | 20 | 51 s | 56.6 | 23 ms | 77 ms | 0 | 2862 créditos e débitos em 20 contas; 0 respostas 429 |
| Alcance depois de religar o consolidado | 20 | 21 s | — | 21 s | 21 s | 0 | latências = tempo até a posição aplicar o último lançamento da conta; 20 contas conferidas, 0 divergências de saldo; todas alcançaram em 21 s |

Os números vêm de um notebook com o ambiente local (docker compose) e servem de referência e para encontrar o gargalo, não como capacidade de produção. Os resumos brutos do k6 de cada cenário estão nesta pasta (`*.json`).
