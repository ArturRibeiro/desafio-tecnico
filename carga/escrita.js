// Cenário: escrita em muitas contas. Cada VU credita e debita na sua conta, com créditos maiores que os débitos,
// para nenhum débito ser recusado. Também é o cenário 4 (consolidado parado), com CENARIO=consolidado-parado.
// Variáveis: VUS (padrão 20), RAMPA, PATAMAR, PAUSA (segundos entre iterações, padrão 0.5).

import { sleep } from 'k6';
import { Counter } from 'k6/metrics';
import * as comum from './comum.js';

const VUS = Number(__ENV.VUS || 20);
const PAUSA = Number(__ENV.PAUSA || 0.5);
const NOME = __ENV.CENARIO || 'escrita';
const TITULO = NOME === 'consolidado-parado' ? 'Escrita com o consolidado parado' : 'Escrita em muitas contas';

const debitosRecusados = new Counter('debitos_recusados');

export const options = {
  setupTimeout: '5m',
  summaryTrendStats: comum.estatisticas,
  scenarios: {
    escrita: { executor: 'ramping-vus', stages: comum.estagios(VUS), exec: 'escrever' },
  },
  thresholds: {
    erros_do_servidor: ['count<1'],
    limite_excedido: ['count<1'],
    debitos_recusados: ['count<1'],
    'http_req_duration{operacao:escrita}': ['max>=0'],
    'http_reqs{operacao:escrita}': ['count>=0'],
  },
};

export function setup() {
  return { contas: comum.abrirContas(VUS) };
}

// Cada iteração faz duas requisições; com a pausa, o VU fica abaixo do limite de 10 por segundo do seu titular.
export function escrever(dados) {
  const conta = dados.contas[__VU - 1];
  const tokenDoTitular = comum.token(__VU);
  comum.creditar(tokenDoTitular, conta, 100.0);
  if (comum.debitar(tokenDoTitular, conta, 30.0).status === 422) {
    debitosRecusados.add(1);
  }
  sleep(PAUSA);
}

export function handleSummary(data) {
  const operacoes = comum.contagem(data, 'http_reqs{operacao:escrita}');
  const arquivos = comum.registrarResultado(data, {
    nome: NOME,
    cenario: TITULO,
    metricaDeVazao: 'http_reqs{operacao:escrita}',
    observacao: `${operacoes} créditos e débitos em ${VUS} contas; ${comum.contagem(data, 'limite_excedido')} respostas 429`,
  });
  // Contas e titulares usados, para a verificação de consistência (alcance.js).
  arquivos[`.trabalho/contas-${NOME}.json`] = JSON.stringify(data.setup_data.contas);
  return arquivos;
}
