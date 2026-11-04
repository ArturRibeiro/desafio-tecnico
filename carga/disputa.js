// Cenário: débitos concorrentes numa mesma conta. A conta tem saldo para metade dos débitos, disparados ao mesmo
// tempo; Lançamentos os serializa (SELECT ... FOR UPDATE), então só os que cabem no saldo podem ser aceitos.
// Todos os débitos usam o titular da conta e cabem no limite por titular do gateway (100 a cada 10 s).
// Variáveis: DEBITOS (padrão 60, no máximo 100).

import { Counter } from 'k6/metrics';
import * as comum from './comum.js';

const DEBITOS = Math.min(Number(__ENV.DEBITOS || 60), 100);
const VALOR = 10.0;
const ESPERADOS = Math.floor(DEBITOS / 2);

const aceitos = new Counter('debitos_aceitos');
const recusados = new Counter('debitos_recusados');
const saldoIncorreto = new Counter('saldo_final_incorreto');

export const options = {
  setupTimeout: '2m',
  summaryTrendStats: comum.estatisticas,
  scenarios: {
    disputa: { executor: 'shared-iterations', vus: DEBITOS, iterations: DEBITOS, exec: 'debitar' },
  },
  thresholds: {
    erros_do_servidor: ['count<1'],
    limite_excedido: ['count<1'],
    saldo_final_incorreto: ['count<1'],
    debitos_aceitos: [`count==${ESPERADOS}`],
    'http_req_duration{operacao:escrita}': ['max>=0'],
    'http_reqs{operacao:escrita}': ['count>=0'],
  },
};

export function setup() {
  const tokenDoTitular = comum.token(1);
  const conta = comum.abrirConta(tokenDoTitular);
  comum.creditar(tokenDoTitular, conta, ESPERADOS * VALOR, 'preparo');
  return { conta };
}

export function debitar(dados) {
  const r = comum.debitar(comum.token(1), dados.conta, VALOR);
  if (r.status === 201) {
    aceitos.add(1);
  } else if (r.status === 422) {
    recusados.add(1);
  }
}

// Saldo final em Lançamentos, por um crédito-sonda de R$ 0,01: com os débitos certos aceitos, o saldo era zero.
export function teardown(dados) {
  const sonda = comum.creditar(comum.token(1), dados.conta, 0.01, 'preparo');
  if (comum.centavos(sonda.saldo) !== 1) {
    saldoIncorreto.add(1);
  }
}

export function handleSummary(data) {
  const aceitosNoTeste = comum.contagem(data, 'debitos_aceitos');
  const recusadosNoTeste = comum.contagem(data, 'debitos_recusados');
  const saldoCorreto = comum.contagem(data, 'saldo_final_incorreto') === 0;
  return comum.registrarResultado(data, {
    nome: 'disputa',
    cenario: 'Débitos concorrentes na mesma conta',
    vus: DEBITOS,
    metricaDeVazao: 'http_reqs{operacao:escrita}',
    observacao: `${aceitosNoTeste} aceitos e ${recusadosNoTeste} recusados por saldo, de ${DEBITOS} simultâneos (esperados ${ESPERADOS} aceitos); saldo final ${saldoCorreto ? 'correto' : 'INCORRETO'}. Serializados de propósito`,
  });
}
