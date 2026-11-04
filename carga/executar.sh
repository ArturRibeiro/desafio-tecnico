#!/usr/bin/env bash
# Teste de carga contra o ambiente local (docker compose), com o ambiente já no ar.
#   carga/executar.sh
# Roda os quatro cenários e a verificação de consistência e grava a evidência em carga/resultados/.
# Parâmetros dos cenários por variáveis de ambiente (veja DESENVOLVIMENTO.md, "Teste de carga").
set -uo pipefail

cd "$(dirname "$0")/.."

export CARGA_UID="${CARGA_UID:-$(id -u)}"
export CARGA_GID="${CARGA_GID:-$(id -g)}"
export EXECUCAO="${EXECUCAO:-$(date +%s)}"

TITULARES="${TITULARES:-200}"
PAUSA_DO_WORKER="${PAUSA_DO_WORKER:-5}"

TRABALHO=carga/.trabalho
RESULTADOS=carga/resultados
mkdir -p "$TRABALHO" "$RESULTADOS"
rm -f "$TRABALHO"/*.linha.md "$TRABALHO"/contas-*.json "$RESULTADOS"/*.json

falhas=0

# Se o script for interrompido no cenário 4, o posicao-worker não fica parado.
trap 'docker compose start posicao-worker >/dev/null 2>&1' EXIT

# Roda um cenário; o código de saída do k6 indica threshold violado, e o cenário segue registrado na tabela.
rodar() {
  local descricao=$1 script=$2
  shift 2
  local variaveis=()
  # Parâmetros dos cenários definidos no shell seguem para o container do k6.
  for p in VUS VUS_ESCRITA VUS_LEITURA DEBITOS AMOSTRA RAMPA PATAMAR PAUSA PAUSA_LEITURA LIMITE_ALCANCE; do
    [ -n "${!p:-}" ] && variaveis+=(-e "$p=${!p}")
  done
  for v in "$@"; do variaveis+=(-e "$v"); done

  echo "==> $descricao"
  if docker compose run --rm -q -e EXECUCAO "${variaveis[@]}" k6 run -q "$script"; then
    return 0
  fi
  echo "    !! $descricao terminou com threshold violado ou erro (veja a tabela)" >&2
  falhas=$((falhas + 1))
}

echo "==> Conferindo o ambiente"
if ! curl -fsS "http://localhost:${GATEWAY_PORTA:-8080}/health/ready" >/dev/null; then
  echo "O ambiente não está no ar: rode 'docker compose up -d --build' antes." >&2
  exit 1
fi

echo "==> Emitindo $TITULARES tokens"
docker compose run --rm -q emissor-de-tokens --titular "carga-$EXECUCAO" --quantidade "$TITULARES" --validade-minutos 180 \
  >"$TRABALHO/tokens.txt"

rodar "Cenário 1: escrita em muitas contas" escrita.js
rodar "Verificação de consistência do cenário 1" alcance.js ORIGEM=escrita
rodar "Cenário 2: débitos concorrentes na mesma conta" disputa.js
rodar "Cenário 3: leitura junto com escrita" leitura.js
# Espera a posição alcançar a escrita do cenário 3, para o atraso dele não entrar no alcance do cenário 4.
rodar "Verificação de consistência do cenário 3" alcance.js ORIGEM=leitura

echo "==> Cenário 4: parando o posicao-worker"
docker compose stop posicao-worker >/dev/null
rodar "Cenário 4: escrita com o consolidado parado" escrita.js CENARIO=consolidado-parado
sleep "$PAUSA_DO_WORKER"
echo "==> Cenário 4: religando o posicao-worker"
docker compose start posicao-worker >/dev/null
rodar "Cenário 4: alcance depois de religar o consolidado" alcance.js ORIGEM=consolidado-parado

echo "==> Montando $RESULTADOS/resultados.md"
cpu=$(grep -m1 'model name' /proc/cpuinfo 2>/dev/null | cut -d: -f2 | sed 's/^ *//')
nucleos=$(nproc 2>/dev/null || echo '?')
memoria=$(awk '/MemTotal/ { printf "%.0f GB", $2 / 1024 / 1024 }' /proc/meminfo 2>/dev/null)
docker_versao=$(docker version --format '{{.Server.Version}}' 2>/dev/null)

{
  echo "# Resultados do teste de carga"
  echo
  echo "Gerado por \`carga/executar.sh\` em $(date '+%d/%m/%Y %H:%M') (execução $EXECUCAO)."
  echo
  echo "Ambiente: ${cpu:-CPU não identificada} (${nucleos} núcleos), ${memoria:-memória não identificada}, Docker ${docker_versao:-?}; todos os containers e o k6 na mesma máquina."
  echo
  echo "| Cenário | Usuários virtuais | Duração | Vazão (req/s) | Latência p50 | Latência p95 | Erros (5xx) | Observação |"
  echo "|---|---|---|---|---|---|---|---|"
  for nome in escrita alcance-escrita disputa leitura alcance-leitura consolidado-parado alcance-consolidado-parado; do
    if [ -f "$TRABALHO/$nome.linha.md" ]; then
      cat "$TRABALHO/$nome.linha.md"
    else
      echo "| $nome | — | — | — | — | — | — | não executado (veja a saída do script) |"
    fi
  done
  echo
  echo "Os números vêm de um notebook com o ambiente local (docker compose) e servem de referência e para encontrar o gargalo, não como capacidade de produção. Os resumos brutos do k6 de cada cenário estão nesta pasta (\`*.json\`)."
} >"$RESULTADOS/resultados.md"

cat "$RESULTADOS/resultados.md"

if [ "$falhas" -gt 0 ]; then
  echo "!! $falhas etapa(s) com threshold violado ou erro." >&2
  exit 1
fi
