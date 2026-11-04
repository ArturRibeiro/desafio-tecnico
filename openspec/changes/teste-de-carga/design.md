# Design

## Context

- Ambiente local por `docker compose`: só o gateway publica porta; o resto fica na rede interna.
- Toda chamada exige JWT. O `emissor-de-tokens` (perfil `ferramentas`) emite um token por execução, para um titular.
- Gateway limita 100 requisições a cada 10 s por titular (`LimiteDeRequisicoes__Permissoes`/`__Janela`).
- Escritas exigem `Idempotency-Key`; respostas de crédito, débito e estorno trazem `saldoDisponivel` (saldo de Lançamentos após a operação).
- Lançamentos não tem endpoint de leitura de saldo; Posição responde `GET /posicao/contas/{conta}` com `saldo` e `atualizadoAte`, e `404` até a abertura ser projetada.
- Débitos na mesma conta são serializados por `SELECT ... FOR UPDATE`.
- Requisitos: `specs/teste-de-carga/spec.md`.

## Goals / Non-Goals

**Goals:**
- Um comando executa os quatro cenários, a verificação e gera a evidência.
- Números gerados pela execução, nunca digitados à mão.

**Non-Goals:**
- Suporte nativo a Windows (o orquestrador é um script bash; no Windows, via WSL).
- Painéis de métricas em tempo real (Grafana/InfluxDB).

## Decisions

**1. k6 em container, perfil `carga` do compose.** Serviço `k6` com imagem de tag exata, alvo `http://gateway:8080` pela rede interna, montando `carga/` como volume.
- Alternativa: NBomber (C#). Exigiria o SDK para rodar e misturaria carga com a solução; o k6 só precisa do Docker.

**2. Orquestrador `carga/executar.sh`.** O k6 não controla containers, e o cenário 4 precisa parar e religar o `posicao-worker`. O script: emite os tokens, roda cada cenário (`docker compose run --rm k6 run ...`), para e religa o worker, roda a verificação e monta a tabela. Ele continua nos cenários seguintes se um falhar e sai com código de erro no fim.
- Alternativa: tudo num script k6 com cenários paralelos. Não resolve parar o worker.

**3. Tokens em lote no emissor.** Novo argumento `--quantidade N` no `EmissorDeTokens`: emite N tokens para `<titular>-1..N`, um por linha, numa única execução. Gravados em `carga/.trabalho/tokens.txt` (ignorado pelo Git).
- Alternativa: k6 assinar os JWTs com a chave de desenvolvimento. Duplicaria a lógica de emissão fora da ferramenta oficial.
- Alternativa: subir o limite do gateway. Deixaria de medir o sistema com a configuração real.

**4. Ritmo por usuário virtual abaixo do limite.** Cada VU usa o seu titular e um ritmo (pausa entre iterações) que fica abaixo de 10 req/s. Os `429` têm métrica própria e threshold zero.

**5. Cenários.**

| Cenário | Script | Carga |
|---|---|---|
| Escrita em muitas contas | `escrita.js` | Cada VU abre as suas contas no início; créditos maiores que os débitos, para nenhum ser recusado; rampa, patamar e descida |
| Débitos concorrentes na mesma conta | `disputa.js` | Uma conta com saldo para metade dos débitos; todos os VUs debitam ao mesmo tempo; confere aceitos × recusados pelo saldo final |
| Leitura junto com escrita | `leitura.js` | Dois cenários k6 em paralelo: o de escrita e consultas de posição às contas já projetadas |
| Consolidado parado | `escrita.js` + `alcance.js` | Worker parado; a mesma escrita do cenário 1; worker religado; `alcance.js` mede o tempo até a posição alcançar |

Os parâmetros (VUs, durações) têm valores padrão e podem ser trocados por variáveis de ambiente.

**6. Verificação de consistência e tempo de alcance (`alcance.js`).** Para cada conta da amostra, um crédito-sonda de R$ 0,01 devolve o saldo atual de Lançamentos; a seguir, a posição é consultada até ficar igual (com tempo limite). Mede o tempo do religamento até todas as contas baterem e conta as divergências.
- Alternativa: ler o banco de Lançamentos direto. Furaria o isolamento entre os contextos e dependeria de credenciais do banco.
- As contas da amostra vêm do cenário anterior: o `setup()` do k6 abre as contas, e o `handleSummary` grava os ids (`setup_data`) em `carga/.trabalho/`.

**7. Evidência.** Cada `handleSummary` grava:
- o resumo bruto em `carga/resultados/<cenario>.json` (versionado);
- a linha da tabela em `carga/.trabalho/<cenario>.linha.md`.

O `executar.sh` monta `carga/resultados/resultados.md` com o cabeçalho, as linhas, a data e o ambiente (CPU e memória de `/proc`, versão do Docker). Métricas: `http_reqs` (vazão), `http_req_duration` p(50)/p(95), contagem de 5xx e de 429 por métricas próprias.

**8. Publicação.** A tabela é copiada para a ARQUITETURA 3.3, abaixo dos cenários, e a coluna "No desafio" passa a "Medido", com link. A seção 5.3 perde o item do teste de carga. O DESENVOLVIMENTO ganha a seção "Teste de carga".

## Risks / Trade-offs

- [Notebook com 4 CPUs disputadas por todos os containers e pelo próprio k6] → números só de referência, com o aviso no texto; o gargalo observado é o resultado útil.
- [Variação entre execuções] → registrar a data e o ambiente; manter versionada só a última execução.
- [Ritmo por VU limita a carga máxima por titular] → a carga cresce aumentando o número de VUs (e de titulares), não o ritmo de cada um.
- [Conta recém-aberta dá 404 na Posição] → leituras só em contas cuja abertura já foi projetada; 404 contado à parte, fora dos erros.
- [Crédito-sonda altera o saldo] → valor mínimo, em contas criadas pelo próprio teste.
- [Script bash não roda nativo no Windows] → documentar o uso via WSL.

## Migration Plan

Não há migração: só ferramentas e documentação. Para reverter, remover a pasta `carga/`, o perfil `carga` do compose e o argumento `--quantidade` do emissor.

## Open Questions

- Valores padrão de VUs e durações: calibrar na primeira execução para a carga mostrar o gargalo sem saturar o notebook.
