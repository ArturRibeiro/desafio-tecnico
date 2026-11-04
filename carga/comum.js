// Módulo comum dos cenários de carga: tokens, operações da API, métricas e o registro dos resultados.
// Cada usuário virtual (VU) usa o token do seu próprio titular, para a carga não esbarrar no limite por titular do
// gateway, e a conta aberta para esse titular no setup().

import http from 'k6/http';
import { sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { SharedArray } from 'k6/data';

export const ALVO = __ENV.ALVO || 'http://gateway:8080';
const EXECUCAO = __ENV.EXECUCAO || String(Date.now());

const tokens = new SharedArray('tokens', () =>
  open('./.trabalho/tokens.txt').split('\n').map((t) => t.trim()).filter((t) => t.length > 0));

export const errosDoServidor = new Counter('erros_do_servidor');
export const limiteExcedido = new Counter('limite_excedido');
export const posicaoNaoProjetada = new Counter('posicao_nao_projetada');

// 404 (conta ainda não projetada na Posição) e 422 (saldo insuficiente) são respostas esperadas, não falhas.
http.setResponseCallback(http.expectedStatuses({ min: 200, max: 299 }, 404, 422));

/** Token do titular de índice i (1 a N). */
export function token(i) {
  if (i < 1 || i > tokens.length) {
    throw new Error(`Faltam tokens: o índice ${i} passa dos ${tokens.length} emitidos (aumente TITULARES).`);
  }
  return tokens[i - 1];
}

// Chave única por operação, também entre cenários e entre setup(), VUs e teardown(), que têm instâncias próprias
// deste módulo: a idempotência de Lançamentos devolveria o resultado original de uma chave repetida.
const INSTANCIA = Math.random().toString(36).slice(2, 10);
let sequencial = 0;
function chave() {
  sequencial += 1;
  return `carga-${EXECUCAO}-${INSTANCIA}-${__VU}-${sequencial}`;
}

function cabecalhos(tokenDoTitular, comChave) {
  const h = { Authorization: `Bearer ${tokenDoTitular}`, 'Content-Type': 'application/json' };
  if (comChave) {
    h['Idempotency-Key'] = chave();
  }
  return h;
}

// O 404 da espera pela projeção, no preparo, é esperado e não entra na contagem.
function registrar(resposta, operacao) {
  if (resposta.status >= 500 || resposta.status === 0) {
    errosDoServidor.add(1);
  } else if (resposta.status === 429) {
    limiteExcedido.add(1);
  } else if (resposta.status === 404 && operacao !== 'preparo') {
    posicaoNaoProjetada.add(1);
  }
  return resposta;
}

function corpo(resposta) {
  try {
    return resposta.json();
  } catch {
    return null;
  }
}

export function abrirConta(tokenDoTitular) {
  const r = registrar(http.post(`${ALVO}/lancamentos/contas`, null, { headers: cabecalhos(tokenDoTitular, true), tags: { operacao: 'abertura' } }));
  return r.status === 201 ? corpo(r).contaId : null;
}

/** Abre uma conta para cada titular de 1 a n, em lotes paralelos. Devolve os ids na ordem dos titulares. */
export function abrirContas(n) {
  const contas = [];
  for (let inicio = 1; inicio <= n; inicio += 50) {
    const lote = [];
    for (let i = inicio; i < inicio + 50 && i <= n; i++) {
      lote.push(['POST', `${ALVO}/lancamentos/contas`, null, { headers: cabecalhos(token(i), true), tags: { operacao: 'abertura' } }]);
    }
    http.batch(lote).forEach((r) => {
      registrar(r);
      contas.push(r.status === 201 ? corpo(r).contaId : null);
    });
  }
  if (contas.some((c) => c === null)) {
    throw new Error('Falha ao abrir as contas do cenário.');
  }
  return contas;
}

function lancar(tipo, tokenDoTitular, conta, valor, operacao) {
  const r = registrar(http.post(
    `${ALVO}/lancamentos/contas/${conta}/${tipo}`,
    JSON.stringify({ valor, descricao: `carga ${tipo}` }),
    { headers: cabecalhos(tokenDoTitular, true), tags: { operacao } }));
  const c = r.status === 201 ? corpo(r) : null;
  return { status: r.status, saldo: c ? c.saldoDisponivel : null, sequencia: c ? c.sequencia : null };
}

export const creditar = (tokenDoTitular, conta, valor, operacao = 'escrita') => lancar('creditos', tokenDoTitular, conta, valor, operacao);
export const debitar = (tokenDoTitular, conta, valor, operacao = 'escrita') => lancar('debitos', tokenDoTitular, conta, valor, operacao);

/** Saldo da posição consolidada, ou null enquanto a conta não foi projetada. */
export function consultarPosicao(tokenDoTitular, conta, operacao = 'leitura') {
  const r = registrar(http.get(`${ALVO}/posicao/contas/${conta}`, { headers: cabecalhos(tokenDoTitular, false), tags: { operacao } }), operacao);
  return r.status === 200 ? corpo(r).saldo : null;
}

/** Saldo e última sequência aplicada da posição consolidada, ou null enquanto a conta não foi projetada. */
export function consultarAtualizacao(tokenDoTitular, conta, operacao = 'alcance') {
  const r = registrar(http.get(`${ALVO}/posicao/contas/${conta}`, { headers: cabecalhos(tokenDoTitular, false), tags: { operacao } }), operacao);
  if (r.status !== 200) {
    return null;
  }
  const c = corpo(r);
  return { saldo: c.saldo, sequencia: c.atualizadoAte.sequencia };
}

/** Espera a posição de cada conta existir (abertura projetada), até o tempo limite em segundos. */
export function esperarProjecao(contas, indiceDoTitular, limiteEmSegundos = 120) {
  const fim = Date.now() + limiteEmSegundos * 1000;
  contas.forEach((conta, i) => {
    while (consultarPosicao(token(indiceDoTitular(i)), conta, 'preparo') === null) {
      if (Date.now() > fim) {
        throw new Error(`A conta ${conta} não foi projetada em ${limiteEmSegundos} s.`);
      }
      sleep(0.5);
    }
  });
}

export const centavos = (valor) => Math.round(Number(valor) * 100);

/** Estágios de rampa, patamar e descida a partir das variáveis RAMPA, PATAMAR e do total de VUs. */
export function estagios(vus) {
  const rampa = __ENV.RAMPA || '10s';
  return [
    { duration: rampa, target: vus },
    { duration: __ENV.PATAMAR || '30s', target: vus },
    { duration: rampa, target: 0 },
  ];
}

export const estatisticas = ['avg', 'min', 'med', 'p(95)', 'max', 'count'];

function valor(data, metrica, campo, padrao = 0) {
  const m = data.metrics[metrica];
  return m && m.values[campo] !== undefined ? m.values[campo] : padrao;
}

// Até 10 s em milissegundos; acima disso, em segundos.
const ms = (v) => (v >= 10000 ? `${Math.round(v / 1000)} s` : `${Math.round(v)} ms`);

/**
 * Grava o resumo bruto do k6 em resultados/<nome>.json e a linha da tabela em .trabalho/<nome>.linha.md.
 * A latência é a da métrica informada (por padrão, as requisições de escrita), ou "—" se ela não tiver amostras;
 * com metricaDeVazao null, a vazão fica "—" (verificações, em que as requisições são só de acompanhamento).
 */
export function registrarResultado(data, { nome, cenario, observacao, vus, metricaDeLatencia = 'http_req_duration{operacao:escrita}', metricaDeVazao = 'http_reqs' }) {
  const segundos = data.state.testRunDurationMs / 1000;
  const comAmostras = valor(data, metricaDeLatencia, 'count') > 0;
  const linha = [
    cenario,
    vus ?? valor(data, 'vus_max', 'max'),
    `${Math.round(segundos)} s`,
    metricaDeVazao === null ? '—' : valor(data, metricaDeVazao, 'rate').toFixed(1),
    comAmostras ? ms(valor(data, metricaDeLatencia, 'med')) : '—',
    comAmostras ? ms(valor(data, metricaDeLatencia, 'p(95)')) : '—',
    valor(data, 'erros_do_servidor', 'count'),
    observacao,
  ];
  return {
    [`resultados/${nome}.json`]: JSON.stringify(data, null, 2),
    [`.trabalho/${nome}.linha.md`]: `| ${linha.join(' | ')} |\n`,
    stdout: `${cenario}: ${linha.slice(1).join(' | ')}\n`,
  };
}

export const contagem = (data, metrica) => valor(data, metrica, 'count');
export const latencia = (data, metrica, campo) => ms(valor(data, metrica, campo));
