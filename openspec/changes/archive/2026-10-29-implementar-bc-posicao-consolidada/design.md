# Design

## Context

A motivação está em [proposal.md](proposal.md). O comportamento exigido está nas specs `projecao-da-posicao` e `consulta-de-posicao`.

Estado atual:

- Os projetos `Posicao.Dominio`, `Posicao.Aplicacao` e `Posicao.Infraestrutura` estão vazios.
- A `Posicao.Api` só expõe a saúde. O `Posicao.Worker` tem um bus MassTransit ainda sem consumers.
- O gateway já roteia `/posicao/{**resto}` para a `Posicao.Api` sem reescrever o caminho.

Restrições herdadas:

- A modelagem está no [DOMINIO.md](../../../../documentacao/DOMINIO.md), seção 6, e no diagrama [2.3](../../../../documentacao/diagramas/02-ddd-tatico/03-posicao-consolidada-modelo-de-dominio.md).
- Os componentes estão no diagrama [2.4](../../../../documentacao/diagramas/02-ddd-tatico/04-posicao-consolidada-c4-component.md).
- Nenhuma referência entre Bounded Contexts. O que vem de Lançamentos é só o projeto `Contratos.Integracao`.
- MassTransit fica na v8.
- O build trata warnings como erro.

Um fato de Lançamentos sustenta várias decisões abaixo: **a data de competência é sempre do dia corrente (UTC) no momento do registro**, e a sequência segue a ordem de registro. Logo, ao longo da sequência de uma conta, o dia de competência nunca diminui.

## Goals / Non-Goals

**Goals:**

- Projeção correta com entrega "pelo menos uma vez" e sem ordem garantida, sem depender do MassTransit para ordenar.
- Uma consulta de posição custa poucas leituras, independentemente do tamanho do histórico.
- Reaproveitar os padrões de Lançamentos, cada contexto com o seu próprio código:
  - `Resultado`;
  - Problem Details com `codigo`;
  - migrações na subida;
  - autenticação de `Plataforma.Hospedagem`.

**Non-Goals:**

- Garantir a posição diante de eventos perdidos para sempre. A recuperação por reenvio a partir de Lançamentos fica como evolução.
- Corrigir lançamentos com competência retroativa. Lançamentos não os aceita (risco R4).

## Decisions

### D1. Domínio

`Posicao.Dominio` tem Value Objects próprios, como manda a regra de não haver Shared Kernel:

- `ContaId`, `TitularId`, `Valor` (decimal com 2 casas, moeda `BRL`) e `DataFechamento` (`DateOnly` em UTC);
- os tipos `Resultado` e `Falha`.

Os Aggregates e a Entity:

| Elemento | Responsabilidade |
|---|---|
| `Titularidade` (Aggregate Root) | Guarda `ContaId`, `TitularId` e `DataAbertura`, e responde `PertenceA(titularId)`. Também guarda `UltimaSequenciaAplicada` e `AtualizadoAte`, o estado da projeção da conta: ficam aqui porque existem antes do primeiro fechamento. |
| `Movimento` (Entity) | Cópia de um lançamento: sequência, `LancamentoId`, tipo, valor, `DataCompetencia`, `DataRegistro`, `LancamentoEstornadoId` e `Efeito` (crédito ou débito). |
| `FechamentoDiario` (Aggregate Root) | `Aplicar(movimento)` soma ao total de créditos ou de débitos conforme o `Efeito` e mantém `SaldoFechamento = abertura + créditos − débitos`. |

**Regras de aplicação.** Um serviço de domínio, `ProjecaoDaConta`, recebe a `Titularidade`, os movimentos pendentes em ordem e o último fechamento:

- Aplica os movimentos enquanto a sequência for `UltimaSequenciaAplicada + 1` e para na primeira lacuna.
- Quando o dia de competência muda, abre um novo fechamento com abertura igual ao fechamento anterior.
- O efeito de um estorno é o inverso do efeito do lançamento estornado. Ele é resolvido na aplicação, e não no recebimento, porque o original tem sequência menor e, portanto, já está aplicado.

*Alternativa descartada:* uma coluna `TotalEstornos`. Não muda o saldo e complicaria o modelo do DOMINIO.

### D2. Ordem, pendência e idempotência no banco

Todo evento recebido vira primeiro uma linha de `Movimento`. O `ContaAberta` vira a `Titularidade`. Depois, a aplicação processa os pendentes da conta. Assim:

- **Pendente** é simplesmente um `Movimento` com sequência maior que `UltimaSequenciaAplicada`. Não há fila em memória nem retentativa artificial à espera da lacuna.
- **Idempotência** em duas barreiras:
  - o Inbox do MassTransit descarta o `MessageId` repetido, que Lançamentos define como o `eventoId`;
  - a chave primária `(conta_id, sequencia)` em `movimentos` e o índice único em `lancamento_id` descartam o que escapar. A violação de unicidade é tratada como "já recebido".
- **Concorrência:** cada consumer roda numa transação que começa com `pg_advisory_xact_lock` sobre o `contaId`. Isso serializa os eventos da mesma conta, inclusive quando a `Titularidade` ainda não existe e não há linha para `SELECT ... FOR UPDATE`. Contas diferentes não disputam o lock.

*Alternativa descartada:* devolver o evento fora de ordem para a fila, com retentativa, até a lacuna fechar. Isso gasta tentativas, pode mandar um evento válido para a `_error` e atrasa toda a fila.

### D3. Consumo com MassTransit

No `Posicao.Worker`, `ConsumidorDeContaAberta` e `ConsumidorDeMovimentacaoRegistrada` fazem só três coisas:

1. validar e traduzir o contrato para o domínio, numa camada anticorrupção em `Posicao.Infraestrutura`;
2. chamar os casos de uso `ProjetarAbertura` e `ProjetarMovimentacao`;
3. gravar.

A configuração de entrega:

- **Inbox:** `AddEntityFrameworkOutbox<PosicaoDbContext>` com `UseEntityFrameworkOutbox` nos endpoints, em `ReadCommitted`. O registro do Inbox e as linhas da projeção são gravados na mesma transação.
- **Filas:** `{prefixo}-conta-aberta` e `{prefixo}-movimentacao-registrada`, declaradas explicitamente. O prefixo (`Consumo:PrefixoDasFilas`, padrão `posicao`) dá filas próprias a cada teste.
- **Retentativas:** `UseMessageRetry` exponencial, com 5 tentativas entre 1 s e 30 s. Esgotadas, o MassTransit move a mensagem para a fila `<endpoint>_error`.
- **Eventos inválidos:** a tradução lança `EventoInvalidoException` quando o valor é ≤ 0, o tipo é desconhecido, a moeda não é `BRL` ou a sequência é inválida. A retentativa ignora essa exceção, então o evento vai direto para a `_error`.
- **Concorrência:** `ConcurrentMessageLimit` configurável, com padrão 16. O advisory lock garante a ordem por conta mesmo assim.

### D4. Persistência

`PosicaoDbContext` (EF Core 10 + Npgsql), no esquema padrão do banco de Posição:

| Tabela | Chave | Índices |
|---|---|---|
| `titularidades` | `conta_id` | — |
| `movimentos` | `(conta_id, sequencia)` | único `lancamento_id`; `(conta_id, data_competencia)` |
| `fechamentos_diarios` | `(conta_id, data)` | — |

Somam-se as tabelas do Inbox do MassTransit (`InboxState` e as do Outbox). O valor de um movimento é `numeric(14,2)`, como em Lançamentos; saldos e totais dos fechamentos são `numeric(19,2)`, porque somam muitos lançamentos.

As migrações ficam em `Posicao.Infraestrutura/Migracoes`, marcadas como código gerado, como em Lançamentos. Quem as aplica:

- o **Worker**, na subida (`Banco__AplicarMigracoesNaSubida=true`), com retentativa em segundo plano;
- a **Api** só verifica (`migracoes` na prontidão), porque não é dona da escrita.

O `AplicadorDeMigracoes` e a `VerificacaoDeMigracoes` são copiados do padrão de Lançamentos para `Posicao.Infraestrutura`.

*Alternativa descartada:* movê-los para `Plataforma.Hospedagem`. Isso levaria o EF Core para a biblioteca de hospedagem, que o Gateway também usa.

### D5. Cálculo da posição

`ObterPosicaoEm(titularId, contaId, instante)`:

1. Carrega a `Titularidade`. Se não existir ou não pertencer ao titular, devolve `conta-nao-encontrada`.
2. Calcula `abertura` = `SaldoFechamento` do último fechamento com data anterior ao dia de `instante`, ou zero.
3. Calcula `saldo` = `abertura` + a soma dos efeitos dos movimentos aplicados (`sequencia <= UltimaSequenciaAplicada`) com competência entre o início do dia de `instante` e `instante`.
4. Devolve o `saldo` e `atualizadoAte = (UltimaSequenciaAplicada, AtualizadoAte)`.

São no máximo três leituras indexadas, com custo constante em relação ao histórico.

`ObterFechamentosDiarios` lê `fechamentos_diarios` por `(conta_id, data)` no intervalo, com `LIMIT tamanho + 1` para saber se há uma próxima página.

### D6. Cache Redis do saldo de abertura

O cache guarda o resultado do passo 2 do D5:

- **Chave:** `posicao:abertura:{contaId}:{data}`.
- **Só é gravado quando é definitivo**, isto é, quando a conta já tem algum movimento aplicado com competência no dia `data` ou depois. Pela propriedade do Context, nenhum movimento futuro pode cair num dia anterior. Por isso, uma movimentação nova nunca deixa uma entrada desatualizada.
- **Expiração:** 24 h.

**Resiliência:**

- Acesso direto pelo `StackExchange.Redis` (`IConnectionMultiplexer`), com timeouts de conexão e de operação de 250 ms e sem abortar a conexão. Sem connection string, o cache fica desligado.
- Qualquer exceção do cache vira um aviso no log e uma leitura no banco. A consulta nunca falha por causa do cache.

*Alternativa descartada:* guardar a posição pronta por instante. São chaves demais, e cada uma ficaria inválida a cada movimento do dia.

### D7. API HTTP

As rotas ficam num grupo `/posicao` com `ExigirEscopo("posicao:ler")`:

| Rota | Resposta `200` |
|---|---|
| `GET /posicao/contas/{contaId:guid}?instante=` | `{ contaId, instante, saldo, moeda, atualizadoAte: { sequencia, instante } }` |
| `GET /posicao/contas/{contaId:guid}/fechamentos?de=&ate=&pagina=1&tamanhoPagina=30` | `{ itens: [{ data, saldoAbertura, totalCreditos, totalDebitos, saldoFechamento }], pagina, tamanhoPagina, temProximaPagina, atualizadoAte }` |

**Leitura dos parâmetros:**

- O `instante` é lido como texto e aceito só no formato ISO 8601 com fuso. Se for posterior a `TimeProvider.GetUtcNow()`, a resposta é `instante-invalido`.
- `de` e `ate` usam o formato `yyyy-MM-dd`. Qualquer parâmetro fora da regra produz `periodo-invalido`.

**Erros:** o mesmo esquema de Problem Details de Lançamentos, com `codigo`, `correlacaoId` e sem detalhes em respostas 5xx, implementado em `Posicao.Api/Http`.

| Código | HTTP |
|---|---|
| `conta-nao-encontrada` | 404 |
| `instante-invalido` | 400 |
| `periodo-invalido` | 400 |

**Ambiente:** a Posicao.Api passa a receber `autenticacao.env` no compose.

### D8. Reconstrução

O comando é `dotnet Posicao.Worker.dll reconstruir`, ou `docker compose run --rm posicao-worker reconstruir`. Ele roda sem iniciar o bus e, para cada conta:

1. pega o advisory lock da conta;
2. apaga os fechamentos;
3. zera `UltimaSequenciaAplicada`;
4. reaplica os movimentos com a mesma `ProjecaoDaConta` do consumo.

Ao final, remove as chaves `posicao:abertura:*` do Redis, se ele estiver configurado. Isso evita servir saldos de abertura calculados a partir de fechamentos corrompidos. O Worker passa a receber `ConnectionStrings__Redis` apenas para esse fim.

Por usar o mesmo código do consumo, a reconstrução produz o mesmo resultado, e repeti-la não muda nada.

### D9. Testes

| Projeto | Cobertura |
|---|---|
| `Posicao.Unidade.Testes` (novo) | Domínio (ordem, lacuna, idempotência, troca de dia, estorno pelo efeito), cálculo da posição e consultas, com portas falsas. |
| `Posicao.Testes` (ampliado) | PostgreSQL, RabbitMQ e Redis reais (Testcontainers). Publica `ContaAbertaV1` e `MovimentacaoRegistradaV1` no broker e verifica pela `Posicao.Api`. |

Cenários de `Posicao.Testes`:

- evento repetido;
- evento fora de ordem e evento antes da abertura;
- evento inválido indo para a `_error`;
- 401, 403 e 404;
- período e instante inválidos;
- Redis parado;
- reconstrução;
- `migracoes` na prontidão.

Cada teste usa o seu próprio banco, como em Lançamentos, para evitar interferência. `Arquitetura.Testes` já cobre as camadas de Posição.

## Risks / Trade-offs

- **R1. Evento perdido para sempre deixa a conta parada na lacuna.**
  - As consultas continuam respondendo, com `atualizadoAte` mostrando o atraso.
  - O Worker registra em log as contas com pendência mais antiga que um limite.
  - O reenvio a partir de Lançamentos fica documentado como evolução (DOMINIO, seção 9).
- **R2. O advisory lock serializa rajadas na mesma conta.** É a mesma escolha de Lançamentos: contas diferentes seguem em paralelo.
- **R3. Redis lento atrasa as consultas.** Mitigado pelos timeouts curtos e pelo fallback ao banco. O health check o marca como `Degraded`.
- **R4. Uma futura competência retroativa quebraria a regra do D6 e a monotonicidade dos dias.**
  - `ProjecaoDaConta` recusa um movimento de dia anterior ao último fechamento com a falha `competencia-fora-de-ordem`. O movimento fica pendente, com erro no log, em vez de corromper saldos.
  - Suportar lançamentos retroativos exigirá recalcular os dias seguintes e invalidar o cache.
- **R5. As tabelas do Inbox crescem.** A janela de detecção de duplicatas do MassTransit limpa os registros antigos.

## Descobertas da implementação

- **Cache sem `IDistributedCache`.** A reconstrução precisa apagar as chaves pelo prefixo (`SCAN`), o que o `IDistributedCache` não oferece. Por isso o adaptador usa o `StackExchange.Redis` diretamente. O pacote ficou na linha 2.x (2.13.17).
- **Inbox em `ReadCommitted`.** Com o isolamento padrão do Outbox do MassTransit (`RepeatableRead`), o snapshot da transação seria tirado antes do lock da conta. Uma transação que esperou o lock leria dados desatualizados da transação anterior.
- **Transação do Inbox reaproveitada.** Dentro de um consumer, a unidade de trabalho não abre outra transação: ela pega o lock e grava na transação que o Inbox já abriu. Fora do consumer (reconstrução e testes), a unidade abre e confirma a própria transação.
- **Falha `estorno-sem-original`.** Um estorno cujo original não foi aplicado interrompe a aplicação, como `competencia-fora-de-ordem`. Isso não acontece com eventos válidos, porque o original sempre tem sequência menor.
- **Pools de conexão nos testes.** O pool do Npgsql sobrevive ao descarte das factories. Com um banco por teste, as conexões ociosas esgotavam o limite padrão do PostgreSQL (100). Os testes limpam o pool do próprio banco ao terminar, e o container de teste aceita 300 conexões.
- **A fila `_error` nasce na primeira falha.** O teste do evento inválido trata a fila ainda inexistente como "nada chegou ainda".

## Migration Plan

Não há dados anteriores: o banco de Posição nasce vazio, com a migração inicial. Se Lançamentos já tiver publicado eventos antes de o consumer existir, eles se perderam, porque a fila ainda não existia. Contas abertas antes desta change não aparecem na Posição, o que é aceitável no ambiente local (`docker compose down -v`).

Rollback: voltar a versão do `Posicao.Worker` e da `Posicao.Api`. Lançamentos não é afetado.
