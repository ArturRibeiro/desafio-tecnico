// Verificação de consistência e tempo de alcance. Para cada conta de uma amostra das contas de um cenário anterior
// (ORIGEM), um crédito-sonda de R$ 0,01 devolve o saldo e a sequência atuais de Lançamentos. A posição consolidada é
// consultada até aplicar essa sequência (alcance); aí o saldo das duas precisa ser igual (consistência). Quem não
// alcança no tempo limite conta como atraso, não como divergência.
// Variáveis: ORIGEM (escrita, leitura ou consolidado-parado), AMOSTRA (padrão 20),
// LIMITE_ALCANCE (segundos, padrão 3600).

import { sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import * as comum from './comum.js';

const ORIGEM = __ENV.ORIGEM || 'escrita';
const LIMITE = Number(__ENV.LIMITE_ALCANCE || 3600);
const contas = JSON.parse(open(`./.trabalho/contas-${ORIGEM}.json`));
const AMOSTRA = Math.min(Number(__ENV.AMOSTRA || 20), contas.length);

const tempoAteAlcancar = new Trend('tempo_ate_alcancar', true);
const conferidas = new Counter('contas_conferidas');
const divergencias = new Counter('divergencias');
const naoAlcancaram = new Counter('nao_alcancaram');

export const options = {
  summaryTrendStats: comum.estatisticas,
  scenarios: {
    alcance: { executor: 'per-vu-iterations', vus: AMOSTRA, iterations: 1, maxDuration: `${LIMITE + 60}s`, exec: 'conferir' },
  },
  thresholds: {
    erros_do_servidor: ['count<1'],
    limite_excedido: ['count<1'],
    divergencias: ['count<1'],
    nao_alcancaram: ['count<1'],
    tempo_ate_alcancar: ['max>=0'],
  },
};

export function setup() {
  return { inicio: Date.now() };
}

// Contas espalhadas pelo cenário de origem: a conta de índice i pertence ao titular i + 1.
export function conferir(dados) {
  const indice = Math.floor(((__VU - 1) * contas.length) / AMOSTRA);
  const tokenDoTitular = comum.token(indice + 1);
  const conta = contas[indice];

  const sonda = comum.creditar(tokenDoTitular, conta, 0.01, 'sonda');
  const fim = dados.inicio + LIMITE * 1000;
  conferidas.add(1);
  while (Date.now() < fim) {
    const posicao = comum.consultarAtualizacao(tokenDoTitular, conta);
    if (posicao !== null && posicao.sequencia >= sonda.sequencia) {
      tempoAteAlcancar.add(Date.now() - dados.inicio);
      if (comum.centavos(posicao.saldo) !== comum.centavos(sonda.saldo)) {
        divergencias.add(1);
      }
      return;
    }
    sleep(1);
  }
  naoAlcancaram.add(1);
}

export function handleSummary(data) {
  const titulo = {
    escrita: 'Consistência ao final da escrita',
    leitura: 'Consistência ao final da leitura',
    'consolidado-parado': 'Alcance depois de religar o consolidado',
  }[ORIGEM];
  const total = comum.contagem(data, 'contas_conferidas');
  const divergentes = comum.contagem(data, 'divergencias');
  const atrasadas = comum.contagem(data, 'nao_alcancaram');
  return comum.registrarResultado(data, {
    nome: `alcance-${ORIGEM}`,
    cenario: titulo,
    metricaDeLatencia: 'tempo_ate_alcancar',
    metricaDeVazao: null,
    observacao: `latências = tempo até a posição aplicar o último lançamento da conta; ${total} contas conferidas, ${divergentes} divergências de saldo; `
      + (atrasadas === 0
        ? `todas alcançaram em ${comum.latencia(data, 'tempo_ate_alcancar', 'max')}`
        : `${atrasadas} não alcançaram em ${LIMITE} s`),
  });
}
