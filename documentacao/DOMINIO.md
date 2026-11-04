# Modelagem de Domínio (DDD)

Este documento define a linguagem, os limites entre as partes do sistema e as regras de negócio.

> Ao mudar uma regra de negócio, atualize este documento junto com o código.
>
> Os diagramas estão em [ARQUITETURA.md](ARQUITETURA.md).

**Idioma:** o domínio é escrito em português, na documentação e no código. Os nomes no código não têm acento (`Lancamento`, `Posicao`). Os termos de DDD ficam em inglês, como são conhecidos; o [glossário](#termos-técnicos) explica cada um.

---

## 1. Visão geral do problema

O banco precisa de uma solução que:

1. **Registre movimentações financeiras** (créditos e débitos) nas contas dos clientes.
2. **Informe a posição consolidada** (saldo) de um cliente **em um determinado momento**, inclusive no passado.
3. **Continue funcionando** sob alta demanda, instabilidade ou falhas parciais, sem gerar dados errados.

As duas primeiras necessidades são **diferentes**:

| | Registrar movimentações | Consultar posição consolidada |
|---|---|---|
| Tipo de carga | Escrita | Leitura (volume alto nos picos) |
| Consistência | **Forte**: nada pode se perder ou duplicar | **Eventual** é aceitável (alguns segundos) |
| Impacto de falha | Crítico: dinheiro errado | Alto, mas recuperável (pode ser reconstruído) |

Por isso a solução é dividida em dois **Bounded Contexts**.

### Premissas

O enunciado não diz quem usa o sistema. Estas premissas preenchem essa lacuna:

1. **O Cliente é o usuário do sistema.** Ele registra créditos, débitos e estornos e consulta a posição consolidada.
2. **O Cliente só acessa as próprias contas.** O identificador do cliente vem do token JWT, nunca de um parâmetro da requisição. Cada conta guarda o seu **Titular** (`TitularId`), e os dois Bounded Contexts conferem isso (seções [5.6](#56-verificação-de-titularidade) e [6.7](#67-verificação-de-titularidade)).
3. **Os canais de acesso não fazem parte do modelo.** App, internet banking ou outro canal são só meios de chegar ao sistema.
4. **Registrar e consultar são permissões separadas.** Os tokens têm escopos distintos (`lancamentos:escrever` e `posicao:ler`). Hoje o Cliente tem os dois, mas no futuro um usuário ou sistema pode ter só o de consulta.

---

## 2. Ubiquitous Language

Os mesmos termos valem para o negócio, a documentação e o código.

| Termo | Código | Definição |
|---|---|---|
| **Conta** | `Conta` | Conta do cliente que recebe movimentações. O significado muda conforme o contexto (seção 4). |
| **Titular** | `TitularId` | Cliente dono da conta, identificado pelo Provedor de Identidade (claim `sub` do token), não por dados cadastrais. Cada conta tem um único titular. |
| **Lançamento** | `Lancamento` | Registro de uma movimentação. Depois de gravado, nunca é alterado nem removido. |
| **Crédito** | `TipoLancamento.Credito` | Entrada de valor na conta. |
| **Débito** | `TipoLancamento.Debito` | Saída de valor da conta. |
| **Estorno** | `TipoLancamento.Estorno` | Lançamento que anula outro. É a única forma de "corrigir" um lançamento. |
| **Valor** | `Valor` | Quantia com moeda. Sempre positiva em um lançamento; o tipo define se soma ou subtrai. |
| **Saldo disponível** | `SaldoDisponivel` | Saldo atual da conta, usado para autorizar débitos. |
| **Limite** | `Limite` | Quanto a conta pode ficar negativa. Padrão: zero. |
| **Posição consolidada** | `PosicaoConsolidadaResponse` | Saldo de uma conta em um instante. |
| **Fechamento diário** | `FechamentoDiario` | Resumo de um dia da conta: saldo de abertura, créditos, débitos e saldo de fechamento. |
| **Data de competência** | `DataCompetencia` | Quando a movimentação aconteceu. É a data usada no cálculo da posição. |
| **Data de registro** | `DataRegistro` | Quando o sistema gravou o lançamento. Serve para auditoria. |
| **Sequência** | `Sequencia` | Número crescente e sem falhas dos lançamentos **de uma conta**. Garante a ordem e revela perdas. |
| **Chave de idempotência** | `ChaveIdempotencia` | Identificador enviado pelo cliente para que a mesma solicitação, repetida, gere um único lançamento. |

### Termos técnicos

| Termo | O que significa |
|---|---|
| **Idempotência** | Repetir a mesma operação não muda o resultado: nada é duplicado. |
| **Consistência forte** | O dado está certo assim que a operação termina. |
| **Consistência eventual** | O dado fica certo logo depois, não no mesmo instante. |
| **Evento** | Aviso de que algo aconteceu, como "movimentação registrada". |
| **Broker** | Serviço que transporta os eventos entre os sistemas. Aqui, o RabbitMQ. |
| **Outbox** | Tabela onde o evento é gravado junto com o lançamento, para ser enviado ao broker depois. Assim, nenhum dos dois se perde sem o outro. |
| **Inbox** | Tabela que lembra os eventos já recebidos, para ignorar repetições. |
| **DLQ** (fila `_error`) | Fila para onde vão as mensagens que continuam falhando, sem travar as demais. |
| **Projeção** | Dados de leitura montados a partir dos eventos. |
| **Reserva da linha** (`SELECT ... FOR UPDATE`) | Trava um registro no banco até o fim da operação, para que outra operação na mesma conta espere. |
| **Bounded Context** | Parte do sistema com modelo e linguagem próprios. |
| **Upstream / Downstream** | Quem publica a informação (Upstream) e quem depende dela (Downstream). |
| **Published Language** | Contrato público de troca de dados entre contextos. |
| **Shared Kernel** | Código de domínio compartilhado entre contextos. Aqui, não é usado. |
| **Aggregate / Aggregate Root** | Conjunto de dados gravado como uma unidade e a entrada única para alterá-lo. |
| **Entity / Value Object** | Item com identidade própria / valor definido só pelo conteúdo, como uma quantia. |
| **Repository** | Componente que carrega e grava um Aggregate. |
| **Domain Event / Integration Event** | Aviso interno de um contexto / aviso público enviado a outro contexto. |
| **Domain Service** | Regra de negócio que não pertence a um único Aggregate. |
| **Invariante** | Regra que precisa ser verdadeira sempre. |

---

## 3. Subdomains

| Subdomain | Classificação | Justificativa |
|---|---|---|
| **Lançamentos** | **Core** | É onde está o maior risco: um erro aqui significa dinheiro errado na conta. Recebe o maior investimento em modelagem e testes. |
| **Posição consolidada** | **Supporting** | Essencial para o cliente, mas é calculada a partir dos lançamentos e pode ser reconstruída. |
| **Identidade e acesso** | **Generic** | Problema já resolvido pelo mercado. Não é modelado: usa autenticação JWT padrão. |

---

## 4. Bounded Contexts

### 4.1 Context Map

```mermaid
flowchart LR
    subgraph L["Lançamentos — UPSTREAM"]
        A[Conta]
        E[Lancamento]
    end

    subgraph P["Posição Consolidada — DOWNSTREAM"]
        D[FechamentoDiario]
        T[Titularidade]
    end

    L -- "ContaAberta v1 e MovimentacaoRegistrada v1<br/>(Published Language via RabbitMQ)" --> P
```

- **Relação:** Lançamentos é **Upstream** e Posição Consolidada é **Downstream**.
- **Integração:** **Published Language**. Lançamentos publica Integration Events com versão, definidos em um projeto de contratos. Nenhum outro código é compartilhado.
- **Sem Shared Kernel:** cada contexto tem o próprio modelo, inclusive o próprio `Valor`. Compartilhar o domínio prenderia a evolução de um serviço à do outro.
- **Cada Bounded Context é um microserviço**, com banco de dados próprio.

### 4.2 Por que "Conta" significa coisas diferentes

| Contexto | O que é uma Conta | Pergunta que ela responde |
|---|---|---|
| Lançamentos | Saldo atual + regras de autorização (situação, limite) | "Posso debitar R$ 100 agora?" |
| Posição Consolidada | Linha do tempo de saldos | "Qual era o saldo em 15/03 às 18h?" |

Um modelo único para as duas perguntas ficaria grande e lento. Essa diferença é o principal sinal de que são dois Bounded Contexts.

### 4.3 Aggregates

Um Aggregate é carregado e gravado inteiro. Cada Aggregate Root tem exatamente um Repository, com uma única implementação, e nenhum outro Repository lê ou grava esse Aggregate. Os testes de arquitetura verificam isso.

No código, todo Aggregate Root implementa a interface `IAggregateRoot` do seu Domain, e a interface do Repository fica junto dele (`I<Aggregate>Repository`). Os Repositories servem às escritas. As consultas (Queries) leem em LINQ pelo `IDbContextReadOnly` (`Query<T>()`).

| Contexto | Aggregate Root | Repository | O que ele mantém consistente |
|---|---|---|---|
| Lançamentos | `Conta` | `IContaRepository` | Saldo, limite, situação e sequência da conta (5.1) |
| Lançamentos | `Lancamento` | `ILancamentoRepository` | O próprio lançamento, imutável (5.2) |
| Posição Consolidada | `Titularidade` | `ITitularidadeRepository` | Titular e estado da projeção da conta (6.2) |
| Posição Consolidada | `FechamentoDiario` | `IFechamentoDiarioRepository` | Totais e saldos de um dia da conta (6.1) |
| Posição Consolidada | `Movimento` | `IMovimentoRepository` | A cópia de um lançamento recebido (6.2) |

`Lancamento` e `Movimento` são Aggregates próprios porque são lidos e gravados sozinhos. Colocá-los dentro do Aggregate da conta exigiria carregar todo o histórico a cada operação (seção 8).

Entre `Conta` e `Lancamento`, a consistência vem de três garantias: só a `Conta` cria lançamentos, os dois são gravados na mesma transação e a linha da conta fica reservada durante a operação. Em Posição Consolidada, o Domain Service `ProjecaoDaConta` aplica os movimentos aos fechamentos, e a `Titularidade` guarda a última sequência aplicada.

---

## 5. Contexto de Lançamentos

**Responsabilidade:** registrar movimentações com segurança, ser a **fonte da verdade** financeira e publicar o que aconteceu.

### 5.1 Aggregate Root `Conta`

| Atributo | Descrição |
|---|---|
| `Id` | `ContaId` |
| `TitularId` | Cliente dono da conta, definido na abertura |
| `ChaveAbertura` | Chave de idempotência da abertura, única por titular |
| `DataAbertura` | Momento da abertura |
| `SaldoDisponivel` | Saldo disponível atual |
| `Limite` | Quanto o saldo pode ficar abaixo de zero. Toda conta nasce com limite **zero**; alterá-lo é uma decisão de crédito do banco, fora do escopo, e nunca vem do cliente |
| `Situacao` | `Ativa`, `Bloqueada`, `Encerrada` |
| `UltimaSequencia` | Última sequência de lançamento emitida |
| `Versao` | Concorrência otimista, como proteção extra à reserva da linha (seção 8) |

**Invariantes:**

1. Uma conta que não está `Ativa` não aceita lançamentos.
2. Um débito não pode deixar `SaldoDisponivel` abaixo de `-Limite`.
3. Todo lançamento recebe `Sequencia = UltimaSequencia + 1`, sem falhas.
4. O saldo só muda por meio de um lançamento: não existe "ajuste de saldo" direto.
5. O `TitularId` é definido na abertura e não muda.

**Decisão: o Aggregate não contém a lista de lançamentos.**
Uma conta pode ter milhões de lançamentos, e carregá-los a cada operação seria inviável. A `Conta` guarda só o saldo e a versão e **produz** um `Lancamento` a cada operação. Os dois são gravados na **mesma transação** (4.3).

### 5.2 Aggregate Root `Lancamento`

| Atributo | Descrição |
|---|---|
| `Id` | `LancamentoId` |
| `ContaId` | Conta à qual pertence |
| `Tipo` | `Credito`, `Debito`, `Estorno` |
| `Valor` | `Valor` (sempre positivo) |
| `Descricao` | Texto descritivo, com tamanho limitado |
| `DataCompetencia` | Data de competência |
| `DataRegistro` | Data de registro |
| `Sequencia` | Ordem do lançamento na conta |
| `LancamentoEstornadoId` | Lançamento estornado (só para `Estorno`) |
| `ChaveIdempotencia` | Chave de idempotência da solicitação |
| `SaldoDisponivelApos` | Saldo logo após este lançamento. Permite repetir a resposta original numa solicitação repetida e serve à auditoria |

**Regras:**

- É **imutável**: não tem métodos de alteração.
- Só é criado pela `Conta`, na mesma transação que atualiza a conta.
- `DataCompetencia` não pode estar no futuro nem antes do início do dia corrente (UTC). Assim, dias já fechados na posição nunca mudam.
- Um lançamento só pode ser estornado **uma vez**.
- Um estorno não pode ser estornado.
- O estorno faz o efeito inverso do original e segue as mesmas regras da conta: estornar um crédito é um débito e pode esbarrar em saldo insuficiente.

### 5.3 Value Objects

| Value Object | Regras |
|---|---|
| `Valor` | Quantia de um lançamento: `decimal` maior que zero, com até 2 casas e no máximo `999.999.999.999,99`, em `BRL`. Nunca é arredondado (`10.005` é recusado) e nunca usa `double`. Saldo e limite não são `Valor`, porque podem ser zero ou negativos. |
| `ContaId`, `LancamentoId` | Identificadores com tipo próprio (GUID v7). Não são sequenciais, para não revelar volume nem permitir adivinhar contas; a ordem por tempo do v7 mantém os índices do banco eficientes. |
| `TitularId` | Texto vindo do Provedor de Identidade (claim `sub`), obrigatório e com tamanho limitado. O sistema não interpreta o formato: só compara. |
| `ChaveIdempotencia` | Obrigatória, de 1 a 100 caracteres ASCII visíveis; única por conta (lançamentos) ou por titular (abertura). |
| `Descricao` | Obrigatória, até 200 caracteres. |
| `TipoLancamento` | `Credito`, `Debito`, `Estorno`. |

### 5.4 Comandos (casos de uso)

| Comando | Efeito | Falhas de negócio possíveis |
|---|---|---|
| `RegistrarCredito` | Cria um lançamento de crédito | Conta inexistente, conta inativa, valor inválido |
| `RegistrarDebito` | Cria um lançamento de débito | As anteriores + **saldo insuficiente** |
| `EstornarLancamento` | Cria um estorno de um lançamento | Lançamento inexistente, já estornado, saldo insuficiente |
| `AbrirConta` | Cria uma conta `Ativa`, com saldo e limite zero, tendo o cliente autenticado como titular | Nenhuma: repetir a mesma chave devolve a conta já aberta |

Todos os comandos recebem o `TitularId` do cliente autenticado, lido do token pela API.

Repetir a mesma `ChaveIdempotencia` devolve o **resultado original**, sem criar outro lançamento. A mesma chave com **outro conteúdo** é recusada (`chave-idempotencia-conflitante`). Operações recusadas não ficam registradas: a repetição é avaliada de novo.

**Códigos de falha.** Cada falha de negócio tem um código fixo, devolvido no campo `codigo` das respostas de erro (Problem Details):

| Código | Quando | HTTP |
|---|---|---|
| `conta-nao-encontrada` | Conta inexistente **ou** de outro titular (seção 5.6) | 404 |
| `lancamento-nao-encontrado` | Lançamento a estornar inexistente ou de outra conta | 404 |
| `saldo-insuficiente` | O saldo ficaria abaixo de `-Limite` | 422 |
| `conta-inativa` | A conta não está `Ativa` | 422 |
| `data-competencia-invalida` | Competência no futuro ou antes do início do dia corrente (UTC) | 422 |
| `estorno-nao-estornavel` | Tentativa de estornar um estorno | 422 |
| `lancamento-ja-estornado` | O lançamento já tem estorno | 409 |
| `chave-idempotencia-conflitante` | Chave já usada com outro conteúdo | 409 |
| `valor-invalido`, `descricao-invalida` | Entrada fora das regras dos Value Objects | 400 |
| `chave-idempotencia-obrigatoria`, `chave-idempotencia-invalida` | Cabeçalho `Idempotency-Key` ausente ou fora do formato | 400 |
| `requisicao-invalida` | Corpo malformado ou data sem fuso | 400 |

### 5.5 Eventos

| Domain Event (interno) | Integration Event (público) |
|---|---|
| `ContaAberta` | `ContaAberta v1` |
| `LancamentoRegistrado` | `MovimentacaoRegistrada v1` |
| `LancamentoEstornado` | `MovimentacaoRegistrada v1` (com `tipo = Estorno`) |

Os **Domain Events** são disparados pelo Aggregate e ficam dentro do contexto. Eles viram **Integration Events** e são gravados no **Outbox**, na mesma transação do lançamento. O Delivery Service do MassTransit, dentro da `Lancamentos.Api`, publica essas mensagens no RabbitMQ. Assim, o modelo interno pode mudar sem afetar Posição Consolidada.

`ContaAberta v1` é o primeiro evento de toda conta, com `sequencia = 0`, e é o único que leva o `TitularId` para Posição Consolidada. Os lançamentos começam em `sequencia = 1`.

### 5.6 Verificação de titularidade

Antes de executar qualquer comando, o caso de uso carrega a `Conta` e confere `Conta.PertenceA(titularId)`.

- Conta inexistente ou de outro titular dá o mesmo resultado: `ContaNaoEncontrada` (HTTP `404`). Responder `403` revelaria que a conta existe.
- A verificação vem **antes** da checagem da `ChaveIdempotencia`, para que repetir uma chave não devolva o resultado de outra pessoa.
- É uma regra de domínio (método da `Conta`), testada como as demais, e não um filtro só na API.

---

## 6. Contexto de Posição Consolidada

**Responsabilidade:** manter e informar a posição das contas ao longo do tempo, a partir dos eventos de Lançamentos. É um **modelo de leitura**: não autoriza nem recusa movimentações.

### 6.1 Aggregate Root `FechamentoDiario`

Identificado por `ContaId` + `Data`.

| Atributo | Descrição |
|---|---|
| `SaldoAbertura` | Saldo no início do dia (fechamento do dia anterior) |
| `TotalCreditos` | Soma dos créditos do dia |
| `TotalDebitos` | Soma dos débitos do dia |
| `SaldoFechamento` | `SaldoAbertura + TotalCreditos - TotalDebitos` |
| `UltimaSequenciaAplicada` | Última sequência de lançamento aplicada |

Só existe fechamento para os dias (UTC) com movimento. O `SaldoAbertura` de um dia é o `SaldoFechamento` do último dia anterior com movimento, ou zero.

**Invariantes:**

1. Cada lançamento é aplicado **uma única vez**: sequência já aplicada é ignorada, porque uma mensagem pode ser entregue mais de uma vez.
2. Lançamentos são aplicados **em ordem**: se faltar uma sequência, o evento fica pendente até o que falta chegar.
3. `SaldoFechamento` sempre bate com abertura, créditos e débitos.
4. Ao longo da sequência de uma conta, o dia de competência **nunca diminui**, porque Lançamentos só aceita competência no dia corrente. Um movimento com competência antes do último fechamento interrompe a aplicação (`competencia-fora-de-ordem`) em vez de alterar um dia já fechado.

Quem aplica é o Domain Service `ProjecaoDaConta`: ele recebe os movimentos pendentes da conta e os aplica enquanto a sequência for a próxima esperada.

### 6.2 Movimentos projetados

O contexto guarda a **titularidade** de cada conta (`Titularidade`: `ContaId`, `TitularId` e `DataAbertura`), criada a partir de `ContaAberta v1`. Uma movimentação de conta cuja titularidade ainda não chegou fica **pendente**, porque a abertura é a sequência 0.

A `Titularidade` também guarda o estado da projeção: `UltimaSequenciaAplicada` e `AtualizadoAte` (instante de registro da última movimentação aplicada; antes da primeira, o da abertura). Esse estado fica nela porque existe antes do primeiro fechamento.

O contexto guarda ainda uma cópia de leitura de cada lançamento recebido, o Aggregate Root `Movimento` (4.3): conta, sequência, `LancamentoId`, tipo, valor, `DataCompetencia`, `DataRegistro`, `LancamentoEstornadoId` e `Efeito`. Ela permite calcular a posição em um instante **dentro** de um dia. Todo evento recebido vira primeiro um `Movimento`; **pendente** é o `Movimento` com sequência acima da última aplicada.

O `Efeito` (crédito ou débito) é definido na aplicação. O de um **estorno** é o inverso do lançamento que ele anula: o estorno de um débito soma aos créditos do dia, e o de um crédito, aos débitos. O original tem sequência menor, então já foi aplicado quando chega a vez do estorno.

### 6.3 Cálculo da posição em um instante

```
Posição(conta, T) = SaldoFechamento do último dia fechado antes de T
                  + Σ movimentos com DataCompetencia entre o início do dia de T e T
```

- Dias fechados **não mudam** e podem ficar em cache por tempo indefinido.
- Toda resposta informa até qual sequência e instante a posição está atualizada (`atualizadoAte`: a `UltimaSequenciaAplicada` e o `AtualizadoAte` da `Titularidade`). Movimentos pendentes não entram no saldo.
- O saldo de abertura de um dia só vai para o cache quando é **definitivo**: quando a conta já tem movimento aplicado nesse dia ou depois. Pela invariante 4 da seção 6.1, nenhum movimento futuro o altera.

### 6.4 Value Objects

`Valor` e `ContaId` próprios deste contexto (mesmas regras, código independente), e `DataFechamento` (data em UTC).

### 6.5 Consultas

| Consulta | Retorno |
|---|---|
| `ObterPosicaoEm(titularId, contaId, instante)` | Posição no `instante` informado (padrão: agora) e `atualizadoAte` |
| `ObterFechamentosDiarios(titularId, contaId, de, ate)` | Fechamentos diários do período, em páginas |

| Código | Quando | HTTP |
|---|---|---|
| `conta-nao-encontrada` | Conta inexistente, de outro titular ou ainda não projetada (seção 6.7) | 404 |
| `instante-invalido` | Instante sem fuso ou no futuro | 400 |
| `periodo-invalido` | Período ausente, invertido, com mais de 366 dias, ou página fora de 1 a 100 itens | 400 |

### 6.6 Reconstrução

Como os lançamentos não mudam e têm ordem, a posição pode ser **reconstruída do zero** a partir dos eventos.

Hoje, a reconstrução usa os `Movimento`s já recebidos: o comando `reconstruir` do `Posicao.Worker` apaga os fechamentos e reaplica os movimentos de cada conta com o mesmo `ProjecaoDaConta` do consumo. Isso corrige defeitos de cálculo. Recuperar eventos que **nunca chegaram** exige que Lançamentos os reenvie (seção 9).

### 6.7 Verificação de titularidade

Toda consulta confere, pela `Titularidade`, se a conta pertence ao `TitularId` do token. Como em Lançamentos, conta inexistente e conta de outro titular respondem igual (`404`).

Uma conta recém-aberta cuja `ContaAberta v1` ainda não foi processada também responde `404` por alguns instantes. É o efeito visível da consistência eventual e aparece só na primeira consulta de uma conta nova.

Posição Consolidada **não consulta** Lançamentos para saber o titular: isso faria um serviço depender do outro estar no ar.

### 6.8 Conciliação com Lançamentos

O saldo de cada conta é calculado nos dois contextos: em Lançamentos, para autorizar débitos (`SaldoDisponivel`), e aqui, para a posição. A consistência eventual garante que a Posição alcança Lançamentos, mas não que os dois cálculos concordam. Um defeito no cálculo, por exemplo, chegaria ao valor errado sem nenhum aviso.

Por isso, cada `MovimentacaoRegistrada v1` traz o `saldoDisponivelApos`, o saldo calculado por Lançamentos logo após a movimentação. O `Movimento` o guarda como `SaldoInformadoApos`. Ao aplicar o movimento, o `ProjecaoDaConta` compara esse valor com o saldo calculado depois da mesma sequência:

1. A comparação é **por sequência**, nunca "no momento atual": o atraso normal não gera alarme falso.
2. Uma diferença vira uma `Divergencia` (conta, sequência e lançamento), registrada como log de erro com o código `saldo-divergente` e contada na métrica `posicao.conciliacao.divergencias`. Os valores ficam fora do log.
3. A divergência **não bloqueia** a projeção nem altera o saldo calculado: o saldo informado serve só para conferir. A correção é corrigir o defeito e reconstruir (6.6).
4. A reconstrução concilia de novo, o que confirma a correção.
5. Movimentos recebidos sem o campo (publicados antes dele) são aplicados sem comparação.

Depois de uma divergência, as sequências seguintes da conta também divergem, porque a diferença continua no saldo. A primeira sequência apontada indica a origem.

---

## 7. Contrato de integração

Definido no projeto de contratos, com versão e **compatível com versões anteriores**: campos novos são opcionais, e mudanças incompatíveis geram uma nova versão.

**`ContaAberta v1`**

```json
{
  "eventoId": "5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9",
  "tipoEvento": "ContaAberta",
  "versao": 1,
  "contaId": "9b2e4f61-1c7d-4a8e-b3f2-5d6c7e8f9a01",
  "titularId": "auth0|6543a1b2c3d4e5f6a7b8c9d0",
  "sequencia": 0,
  "moeda": "BRL",
  "dataAbertura": "2026-10-27T09:00:00Z",
  "correlacaoId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"
}
```

**`MovimentacaoRegistrada v1`**

```json
{
  "eventoId": "0f8c1d3e-6a2b-4c55-9b1e-2f4a6d7c8e90",
  "tipoEvento": "MovimentacaoRegistrada",
  "versao": 1,
  "contaId": "9b2e4f61-1c7d-4a8e-b3f2-5d6c7e8f9a01",
  "lancamentoId": "3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f",
  "sequencia": 42,
  "tipo": "Debito",
  "valor": "150.00",
  "moeda": "BRL",
  "dataCompetencia": "2026-10-27T14:32:10Z",
  "dataRegistro": "2026-10-27T14:32:10.512Z",
  "lancamentoEstornadoId": null,
  "saldoDisponivelApos": "350.00",
  "correlacaoId": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"
}
```

O `saldoDisponivelApos` é o saldo da conta logo após a movimentação, usado na conciliação ([6.8](#68-conciliação-com-lançamentos)). É opcional: eventos publicados antes dele não o trazem, e a versão continua a 1.

Valores monetários trafegam como **texto decimal** (`"150.00"`), o padrão do MassTransit. Assim, nenhum consumidor precisa converter para ponto flutuante, o que arredondaria valores grandes em linguagens que só têm `double`. Os contratos aceitam ler número ou texto.

Os eventos **não levam dados cadastrais** do cliente (nome, CPF, contato), só identificadores.

Sozinho, o `TitularId` não identifica a pessoa, mas pela LGPD continua sendo dado pessoal. Por isso ele trafega **uma única vez**, em `ContaAberta v1`, e não em cada movimentação.

---

## 8. Decisões de modelagem e alternativas

| Decisão | Alternativa considerada | Motivo da escolha |
|---|---|---|
| Lançamentos só por inclusão, com estorno | Permitir edição ou exclusão | Permite auditar e reconstruir qualquer posição. |
| `Conta` sem a coleção de lançamentos, e `Lancamento` e `Movimento` como Aggregate Roots próprios | Aggregate contendo todos os lançamentos (ou movimentos), como Entities internas | Desempenho: o Aggregate fica pequeno, seja qual for o histórico. A consistência vem da transação única e da reserva da conta (4.3). |
| Reserva da linha da `Conta` (`SELECT ... FOR UPDATE`) em cada operação, com `Versao` como proteção extra | Só concorrência otimista, com retentativas | Operações na mesma conta precisam correr uma de cada vez para o saldo ficar certo. Sob pico, o otimista gera conflitos ou acumula retentativas; a reserva sempre dá sucesso ou falha de negócio e não afeta outras contas. |
| `SaldoDisponivelApos` gravado em cada lançamento | Recalcular o saldo ao repetir a solicitação | A repetição devolve exatamente a resposta original, e o dado serve à auditoria. É gravado uma vez e nunca muda. |
| Saldo disponível mantido em Lançamentos | Consultar Posição Consolidada para autorizar débitos | A autorização não pode depender de outro serviço nem de um dado que pode estar atrasado. |
| Dois Bounded Contexts | Um único modelo | Cargas, consistência e significado de "Conta" diferentes. |
| Published Language, sem Shared Kernel | Biblioteca de domínio compartilhada | Cada contexto evolui de forma independente. |
| Consistência eventual na posição consolidada | Atualização no mesmo instante | Isola falhas: se Posição cair, Lançamentos continua funcionando. O atraso aparece em `atualizadoAte`. |
| `Resultado` para falhas de negócio | Exceções | Saldo insuficiente é um resultado esperado. Exceções ficam para falhas técnicas. |
| `decimal` para dinheiro | `double` / centavos em `long` | Precisão exata e leitura mais fácil que centavos. |
| MassTransit (Transactional Outbox, Inbox, retentativas e DLQ) sobre RabbitMQ | Outbox, Inbox e publicador escritos à mão; cliente `RabbitMQ.Client` puro | Mecanismos já testados, com menos código próprio numa parte crítica. A regra de ordem (`Sequencia`) fica no domínio, porque o MassTransit não garante ordem. |
| Entrega do Outbox dentro da `Lancamentos.Api` | Container `Lancamentos.Worker` só para publicar | A requisição já não depende do broker (só grava no banco), e o MassTransit impede que duas instâncias publiquem a mesma mensagem. É um container a menos; mover a entrega para um Worker é só configuração. |
| Consumer em `Posicao.Worker` separado da `Posicao.Api` | Consumer na própria API | Consumo e consultas escalam e falham de forma independente. |
| MassTransit **v8** (Apache 2.0) | v9 (licença comercial), Wolverine, Rebus | Open source e suficiente para o desafio. Em produção, avaliar o custo da licença da v9 ou migrar para uma alternativa. |
| Conciliação por sequência, com o `saldoDisponivelApos` no próprio evento | Rotina periódica comparando os bancos dos dois contextos; evento separado de "saldo conferido" | Detecta a divergência na movimentação que a causou, sem um serviço chamar o outro, sem um contexto ler o banco do outro e sem alarme falso por atraso. O saldo no evento não é dado novo para a Posição, que já o calcula. |
| `TitularId` na `Conta` e titularidade projetada em Posição via `ContaAberta v1` | Posição consultar Lançamentos a cada requisição; `titularId` em toda `MovimentacaoRegistrada`; lista de contas no token | Um serviço não depende do outro, e trafega o mínimo de dado pessoal. Uma lista de contas no token ficaria desatualizada e aumentaria o token. |
| Domínio e código em português | Código em inglês | Mantém os mesmos termos no negócio, na documentação e no código, sem tradução mental. |

---

## 9. Fora do escopo (evoluções futuras)

- **Histórico de movimentações da conta** (extrato), com as entradas e saídas de valores por período. Os lançamentos já ficam gravados; falta a consulta.
- **Partidas dobradas** (todo lançamento com contrapartida em outra conta), necessárias para transferências entre contas.
- **Múltiplas moedas** e conversão cambial.
- **Lançamentos futuros/agendados** e retroativos além do dia corrente.
- Contexto de **Clientes** (cadastro, dados pessoais), hoje tratado só por identificadores.
- **Reenvio de eventos** por Lançamentos (de uma conta ou de todas), para reconstruir a Posição Consolidada mesmo depois de perder eventos.
