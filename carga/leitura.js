// Cenário: consultas de posição junto com a escrita. Dois grupos de VUs em paralelo: os de escrita creditam e
// debitam; os de leitura consultam a posição. Cada VU usa a conta do seu titular, já projetada no setup().
// Variáveis: VUS_ESCRITA (padrão 10), VUS_LEITURA (padrão 40), RAMPA, PATAMAR, PAUSA, PAUSA_LEITURA (padrão 0.2).

import { sleep } from 'k6';
import * as comum from './comum.js';

const VUS_ESCRITA = Number(__ENV.VUS_ESCRITA || 10);
const VUS_LEITURA = Number(__ENV.VUS_LEITURA || 40);
const PAUSA = Number(__ENV.PAUSA || 0.5);
const PAUSA_LEITURA = Number(__ENV.PAUSA_LEITURA || 0.2);

export const options = {
  setupTimeout: '5m',
  summaryTrendStats: comum.estatisticas,
  scenarios: {
    escrita: { executor: 'ramping-vus', stages: comum.estagios(VUS_ESCRITA), exec: 'escrever' },
    leitura: { executor: 'ramping-vus', stages: comum.estagios(VUS_LEITURA), exec: 'ler' },
  },
  thresholds: {
    erros_do_servidor: ['count<1'],
    limite_excedido: ['count<1'],
    posicao_nao_projetada: ['count<1'],
    'http_req_duration{operacao:escrita}': ['max>=0'],
    'http_req_duration{operacao:leitura}': ['max>=0'],
    'http_reqs{operacao:leitura}': ['count>=0'],
  },
};

export function setup() {
  const contas = comum.abrirContas(VUS_ESCRITA + VUS_LEITURA);
  contas.forEach((conta, i) => comum.creditar(comum.token(i + 1), conta, 1000.0, 'preparo'));
  comum.esperarProjecao(contas, (i) => i + 1);
  return { contas };
}

export function escrever(dados) {
  const conta = dados.contas[__VU - 1];
  const tokenDoTitular = comum.token(__VU);
  comum.creditar(tokenDoTitular, conta, 100.0);
  comum.debitar(tokenDoTitular, conta, 30.0);
  sleep(PAUSA);
}

export function ler(dados) {
  comum.consultarPosicao(comum.token(__VU), dados.contas[__VU - 1]);
  sleep(PAUSA_LEITURA);
}

export function handleSummary(data) {
  const arquivos = comum.registrarResultado(data, {
    nome: 'leitura',
    cenario: 'Leitura junto com escrita',
    metricaDeLatencia: 'http_req_duration{operacao:leitura}',
    metricaDeVazao: 'http_reqs{operacao:leitura}',
    observacao: `latência e vazão das consultas, com ${VUS_LEITURA} VUs de leitura e ${VUS_ESCRITA} de escrita; escrita no mesmo período: p95 ${comum.latencia(data, 'http_req_duration{operacao:escrita}', 'p(95)')}`,
  });
  // Contas do cenário, para a verificação de consistência (alcance.js) antes do cenário seguinte.
  arquivos['.trabalho/contas-leitura.json'] = JSON.stringify(data.setup_data.contas);
  return arquivos;
}
