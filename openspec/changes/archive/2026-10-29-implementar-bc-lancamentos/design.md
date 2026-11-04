# Design

## Context

O que já existe, entregue pela change `estruturar-solucao`:
- os projetos `Lancamentos.{Dominio,Aplicacao,Infraestrutura,Api}`, ainda vazios;
- uma `Lancamentos.Api` que só expõe a saúde e configura um bus do MassTransit 8.5.12 sem publicações;
- `Contratos.Integracao`, ainda vazio;
- `Plataforma.Hospedagem`, com a saúde e a correlação;
- testes de arquitetura que leem os `.csproj`;
- um compose com um PostgreSQL por contexto e credenciais por container.

O modelo de domínio está em [DOMINIO.md](../../../../documentacao/DOMINIO.md), seções 5 e 7. A motivação está em [proposal.md](proposal.md), e os comportamentos exigidos estão nas specs desta change.

Restrições:
- .NET 10;
- MassTransit travado na v8;
- warnings tratados como erro;
- o Domínio sem nenhuma dependência;
- a Aplicação dependendo só do Domínio.

## Goals / Non-Goals

**Goals:**
- Fazer as regras de dinheiro valerem em **duas camadas**: no Aggregate, testado sem infraestrutura, e no banco, com restrições únicas que barram o que escapar por concorrência.
- Garantir que operação e evento sejam confirmados juntos, sem depender do broker na requisição.
- Deixar a autenticação pronta para ser reaproveitada por Posição e pelo gateway, trocando só a configuração.

**Non-Goals:**
- Consultas de saldo e de extrato, que ficam com Posição Consolidada.
- Validação de JWT no gateway e na Posicao.Api, que entram na change de autenticação.
- Comandos de bloqueio, encerramento e alteração de limite. A situação `Bloqueada` existe no modelo e é testada no domínio e por meio do banco, mas nenhuma rota a altera.

## Decisions

### D1. Domínio puro com `Resultado`

- O `Resultado<T>` e a `Falha(Codigo)` ficam no próprio `Lancamentos.Dominio`. O código é uma das constantes de `CodigosDeFalha` (`saldo-insuficiente`, `conta-inativa` etc.), e são esses os valores que aparecem no campo `codigo` das respostas HTTP. Exceções ficam só para falhas técnicas.
- Os Value Objects são `readonly record struct`, com fábrica `Criar(...)` que devolve `Resultado`:
  - `Valor`: `decimal`, maior que zero, no máximo `999999999999.99` e `Math.Round(v, 2) == v`. Assim, `10.500` é aceito e `10.005` é recusado.
  - `ContaId` e `LancamentoId`: GUID v7, aleatório e não consecutivo (`Guid.CreateVersion7()` mantém os índices ordenados no banco sem expor um contador).
  - `TitularId`: texto opaco de 1 a 200 caracteres.
  - `ChaveIdempotencia`: 1 a 100 caracteres ASCII visíveis.
  - `Descricao`: 1 a 200 caracteres depois de `Trim`.
- A `Conta` expõe estes métodos:
  - `Abrir(id, titular, chaveAbertura, agora)`;
  - `RegistrarCredito(...)`, `RegistrarDebito(...)` e `Estornar(original, jaEstornado, ...)`, que devolvem `Resultado<Lancamento>`;
  - `PertenceA(titular)`.
- Os eventos de domínio pendentes (`ContaAberta`, `LancamentoRegistrado`, `LancamentoEstornado`) ficam numa lista que a Infraestrutura esvazia ao confirmar.
- O instante atual (`agora`) é passado como parâmetro. O Domínio não conhece `TimeProvider` nem injeção de dependência. A Aplicação obtém o instante de `TimeProvider`, que os testes substituem.
- **Dado novo no `Lancamento`: `SaldoDisponivelApos`.** Ele permite que a repetição idempotente devolva **o mesmo corpo** da resposta original, com o saldo daquele momento, e também serve à auditoria.
- **Dado novo na `Conta`: `ChaveAbertura`.** É a idempotência da abertura, única por titular.
- O `DOMINIO.md` é atualizado com os dois dados novos.
- **Alternativa descartada:** exceções de domínio (`SaldoInsuficienteException`). Saldo insuficiente é um resultado esperado, e exceção esconderia o fluxo, como já está registrado no `DOMINIO.md`, seção 8.

### D2. Casos de uso e portas

- Cada comando é uma classe em `Lancamentos.Aplicacao`, sem mediator:
  - `AbrirConta`;
  - `RegistrarCredito`;
  - `RegistrarDebito`;
  - `EstornarLancamento`.
- As portas, também na Aplicação e implementadas pela Infraestrutura, são estas:
  - `IRepositorioDeContas`: `ObterParaAlteracaoAsync(contaId)`, com bloqueio, e `ObterPorChaveDeAberturaAsync(titular, chave)`;
  - `IRepositorioDeLancamentos`: `ObterAsync(contaId, id)`, `ObterPorChaveAsync(contaId, chave)` e `ExisteEstornoDeAsync(id)`;
  - `IUnidadeDeTrabalho`: `ExecutarAsync(func)`, que abre a transação, executa e confirma;
  - `IContextoDaRequisicao`: o `CorrelacaoId`.
- A ordem dentro do caso de uso é a das specs:
  1. carregar a conta com bloqueio;
  2. verificar a titularidade e devolver `conta-nao-encontrada` se falhar;
  3. verificar a chave de idempotência e, se a chave já foi usada, comparar o conteúdo;
  4. aplicar as regras do Aggregate;
  5. persistir.
- **Comparação de conteúdo na idempotência:** compara com o lançamento já gravado os campos `tipo`, `valor` e `descricao` (e `lancamentoEstornadoId`, no estorno). A `dataCompetencia` só é comparada quando a requisição a informa, porque o valor padrão é "agora" e mudaria a cada tentativa. Não é preciso guardar hash da requisição.
- **Operações recusadas não ficam registradas.** A repetição reavalia a operação, o que está coberto pela spec.

### D3. Concorrência: bloqueio da linha da conta, com `Versao` de reserva

- Toda alteração carrega a `Conta` com `SELECT ... FOR UPDATE` dentro da transação. Operações na mesma conta passam a ser executadas em série; contas diferentes não disputam entre si.
- `Versao` continua como token de concorrência otimista no EF Core. Se, por algum caminho, uma alteração escapar do bloqueio, o `SaveChanges` falha em vez de sobrescrever o saldo.
- **Por que não só o otimista, como diz o DOMINIO.md hoje:** com 10 débitos simultâneos na mesma conta, o otimista exige retentativas. Com um limite de retentativas, parte das requisições volta como conflito. Sem limite, há risco de fila de retentativas sob pico. A spec exige que as 10 terminem em `201` ou `422`, nunca em conflito. O bloqueio da linha dá esse resultado de forma determinística, e o custo (serializar operações da **mesma** conta) é o mesmo que o otimista pagaria em retentativas.
- A decisão vai para a tabela da seção 8 do `DOMINIO.md` e para a linha correspondente em `ARQUITETURA.md`, seção 3.1.
- **Consequência:** a verificação de idempotência acontece com a conta bloqueada, então duas repetições simultâneas da mesma chave não conseguem criar dois lançamentos. O índice único é a última barreira.
- **Abertura de conta:** não há linha para bloquear. A corrida entre duas aberturas com a mesma chave é resolvida pelo índice único `(titular_id, chave_abertura)`: a violação é capturada, a conta existente é relida e a resposta original é devolvida.

### D4. Persistência

- `LancamentosDbContext` fica em `Lancamentos.Infraestrutura`, com EF Core 10 e o provider Npgsql 10.
- As tabelas e colunas usam `snake_case`, configurado explicitamente no mapeamento, sem pacote de convenção.
- `contas` tem estas colunas:
  - `id`, `titular_id`;
  - `chave_abertura`;
  - `saldo_disponivel numeric(14,2)`, `limite numeric(14,2)`;
  - `situacao`, guardada como texto;
  - `ultima_sequencia`;
  - `versao`, o token de concorrência;
  - `data_abertura`.
- `lancamentos` tem estas colunas:
  - `id`, `conta_id`;
  - `tipo`, `valor`, `descricao`;
  - `data_competencia`, `data_registro`;
  - `sequencia`;
  - `lancamento_estornado_id`;
  - `chave_idempotencia`;
  - `saldo_disponivel_apos`.
- Os índices únicos reforçam no banco as regras das specs:
  - `contas (titular_id, chave_abertura)`;
  - `lancamentos (conta_id, sequencia)`, para a sequência sem duplicidade;
  - `lancamentos (conta_id, chave_idempotencia)`;
  - `lancamentos (lancamento_estornado_id) WHERE lancamento_estornado_id IS NOT NULL`, para o estorno único.
- As tabelas do Outbox do MassTransit (`OutboxMessage`, `OutboxState`, `InboxState`) entram no mesmo `DbContext` por `AddTransactionalOutboxEntities()`.
- As migrações ficam em `Lancamentos.Infraestrutura/Migracoes` e são geradas com `dotnet-ef`, registrado como ferramenta local em `.config/dotnet-tools.json`.
- Uma violação de índice único (`23505`) é traduzida pela Infraestrutura para a falha de negócio correspondente, pelo nome do índice. Por exemplo, o índice de estorno vira `lancamento-ja-estornado`.

### D5. Migrações na subida, sem quebrar a inicialização tolerante

- Um `BackgroundService` (`AplicadorDeMigracoes`) executa `Database.MigrateAsync()` quando `Banco:AplicarMigracoesNaSubida = true`. Se falhar, tenta de novo a cada 5 s. O processo nunca encerra por isso.
- O EF Core 9+ já obtém um lock no banco durante a migração, então várias instâncias não migram ao mesmo tempo.
- A verificação de saúde `migracoes` (tag `ready`) consulta `GetPendingMigrationsAsync()`. Ela fica `Unhealthy` enquanto houver migrações pendentes ou o banco estiver inacessível, e funciona igual quando as migrações são aplicadas por fora.
- No compose e nos testes, a opção fica `true`. O padrão é `false`, porque em produção a recomendação, registrada no README, é aplicar as migrações como um passo separado do deploy, com um bundle do EF.
- **Alternativa descartada:** um container de migração no compose. É mais um container e mais uma imagem com o SDK, só para o ambiente local.

### D6. Autenticação JWT reutilizável

- `Plataforma.Hospedagem` ganha `AdicionarAutenticacaoJwt(configuracao)` e a política por escopo `ExigirEscopo("lancamentos:escrever")`, que lê a claim `scope` separada por espaço. As opções ficam em `Autenticacao:*`:
  - **`Autoridade`** preenchida: descoberta OIDC e JWKS. É o caminho de produção, com o Provedor de Identidade.
  - **`ChaveDeAssinatura`** preenchida, em Base64 com pelo menos 256 bits: HS256 com chave simétrica. É o caminho do ambiente local.
  - Também ficam ali `Emissor` e `Audiencia`, ambos obrigatórios.
- Detalhes da validação:
  - `MapInboundClaims = false`, para que `sub` e `scope` cheguem com o nome original;
  - `ClockSkew` de 30 s;
  - `sub` ausente, vazio ou com mais de 200 caracteres falha na validação do token, com `401`.
- Respostas `401` e `403` sem corpo viram Problem Details por `AddProblemDetails()` com `UseStatusCodePages()`.
- Os endpoints de saúde continuam anônimos, porque não estão no grupo que exige autorização.
- **Emissor de desenvolvimento:**
  - `ferramentas/EmissorDeTokens` é um console com `Microsoft.IdentityModel.JsonWebTokens`.
  - Aceita os argumentos `--titular`, `--escopos` (padrão: `lancamentos:escrever posicao:ler`) e `--validade-minutos` (padrão: 60).
  - Lê a chave, o emissor e a audiência das mesmas variáveis `Autenticacao__*`.
  - No compose, é o serviço `emissor-de-tokens`, no profile `ferramentas`, e roda com `docker compose run --rm emissor-de-tokens --titular cliente-1`. Basta o Docker, como na subida do ambiente.
- A chave de desenvolvimento fica em `ambiente/desenvolvimento/autenticacao.env`, entregue apenas à `lancamentos-api` e ao emissor.
- **Alternativas descartadas:**
  - `dotnet user-jwts`: funciona só com `dotnet run` e guarda a chave nos user-secrets da máquina, não nos containers.
  - Keycloak no compose: realista, mas pesado para o desafio, e é justamente o escopo da change de autenticação.

### D7. API HTTP

- Minimal API em `Lancamentos.Api`, num grupo `/lancamentos` que exige autenticação e o escopo de escrita:

| Rota | Caso de uso | Sucesso |
|---|---|---|
| `POST /lancamentos/contas` | `AbrirConta` | `201` + `Location: /lancamentos/contas/{id}` |
| `POST /lancamentos/contas/{contaId}/creditos` | `RegistrarCredito` | `201` |
| `POST /lancamentos/contas/{contaId}/debitos` | `RegistrarDebito` | `201` |
| `POST /lancamentos/contas/{contaId}/lancamentos/{lancamentoId}/estornos` | `EstornarLancamento` | `201` |

- **Mapeamento de falha para status:**
  - `400`: entrada malformada ou valores fora das regras (`requisicao-invalida`, `valor-invalido`, `descricao-invalida` e `chave-idempotencia-*`, exceto `conflitante`).
  - `404`: `conta-nao-encontrada` e `lancamento-nao-encontrado`.
  - `409`: `lancamento-ja-estornado` e `chave-idempotencia-conflitante`.
  - `422`: as demais regras de negócio (`saldo-insuficiente`, `conta-inativa`, `data-competencia-invalida` e `estorno-nao-estornavel`).
- A `dataCompetencia` é lida por um conversor JSON que exige o fuso no texto (`Z` ou `±hh:mm`). Sem fuso, a resposta é `400 requisicao-invalida`, para evitar que o fuso do servidor seja assumido em silêncio.
- Erros inesperados passam por `UseExceptionHandler` e viram `500` Problem Details, sem detalhes. O erro completo vai só para o log, com o `correlacaoId`.
- A Lancamentos.Api também usa `UsarCorrelacao()`, de modo que chamadas diretas pela rede interna também ganham `correlacaoId`.

### D8. Eventos e Outbox

- `Contratos.Integracao/Lancamentos` define `ContaAbertaV1` e `MovimentacaoRegistradaV1`, `sealed record` com exatamente os campos do `DOMINIO.md`, seção 7. `TipoEvento` e `Versao` são propriedades com valor fixo.
- A tradução de Domain Event para Integration Event fica em `Lancamentos.Infraestrutura` (`TradutorDeEventos`), daí a mudança na spec `qualidade-de-build`. O diagrama 2.2 é atualizado para mostrar o componente na Infraestrutura.
  - **Alternativa descartada:** traduzir na Aplicação. Isso faria o modelo de casos de uso conhecer a Published Language, e qualquer versão nova do contrato tocaria a Aplicação.
- `AddEntityFrameworkOutbox<LancamentosDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); })`:
  - O `IPublishEndpoint` publica dentro do escopo da requisição, e o MassTransit grava em `OutboxMessage` no mesmo `SaveChanges` e na mesma transação.
  - O Delivery Service, dentro da própria API, entrega as mensagens ao RabbitMQ.
- O `MessageId` da mensagem é o `EventoId`, definido no callback de publicação. Assim, toda reentrega tem o mesmo identificador, tanto na mensagem quanto no corpo.
- O exchange usa o nome padrão do MassTransit, derivado do tipo (`Contratos.Integracao.Lancamentos:MovimentacaoRegistradaV1`). O consumidor de Posição usa o mesmo tipo de contrato, então os nomes coincidem sem configuração extra.

### D9. Testes

| Projeto | Cobre | Como |
|---|---|---|
| `Lancamentos.Unidade.Testes` (novo) | Domínio (invariantes, Value Objects, estorno, data de competência, titularidade, eventos de domínio) e casos de uso (ordem titularidade → idempotência → regras, comparação de conteúdo) | Unitário puro, com instante fixo e portas falsas em memória |
| `Lancamentos.Api.Testes` | Specs `autenticacao`, `titularidade`, `contas`, `lancamentos`, `eventos-de-integracao` e a saúde modificada | `WebApplicationFactory` com PostgreSQL e RabbitMQ reais (Testcontainers) e tokens emitidos pelo próprio teste com uma chave de teste |
| `Arquitetura.Testes` | Spec `qualidade-de-build` modificada | Regra da Infraestrutura atualizada e uma nova regra: Domínio e Aplicação sem dependência de `Contratos.Integracao` |

- **Concorrência:** 10 débitos em paralelo com `Task.WhenAll` sobre um `HttpClient`.
- **Outbox com broker fora:** o RabbitMQ do teste usa uma porta fixa no host (`WithPortBinding`), para manter o endereço após `StopAsync`/`StartAsync`. Um consumidor feito com `RabbitMQ.Client` liga uma fila temporária ao exchange do evento e lê as mensagens.
- **Banco que sobe depois:** a API inicia apontando para um PostgreSQL ainda parado, também com porta fixa, e o teste confere `migracoes = Unhealthy`, depois inicia o container e espera `200`.

## Risks / Trade-offs

- **[Risco] `MassTransit.EntityFrameworkCore` 8.5.12 foi publicado antes do EF Core 10 e pode exigir outra versão do EF Core ou falhar em tempo de execução.** → Mitigação: a primeira tarefa verifica restore, build e um teste do Outbox com EF Core 10. Se houver incompatibilidade, o plano B é EF Core 9 com Npgsql 9 sobre `net10.0`, ambos suportados. A decisão fica registrada no design e no `Directory.Packages.props`. Ir para o MassTransit v9 **não** é opção, pela licença.
- **[Risco] Contas muito movimentadas viram gargalo com `FOR UPDATE`.** → Aceito: operações na mesma conta já precisam ser serializadas para o saldo estar correto, e o bloqueio dura só a transação curta. Com mais tempo, avaliaríamos particionar contas de altíssimo volume (por exemplo, subcontas de liquidação).
- **[Risco] Chave HS256 compartilhada entre o emissor e a API.** → Ela só existe no ambiente local. Em produção, `Autoridade` usa chaves assimétricas do Provedor de Identidade, e a chave simétrica fica sem uso. O README avisa.
- **[Risco] Testes com portas fixas colidem com algo na máquina ou no CI.** → Mitigação: a porta é escolhida livre no início do teste (bind em `:0`, lê a porta e libera) e só então o container é criado com ela.
- **[Trade-off] O Outbox entrega pelo menos uma vez e sem ordem global garantida.** → É o esperado: o `eventoId` permite descartar duplicatas e a `sequencia` permite ordenar. Ambos são responsabilidade do consumidor, na change de Posição.
- **[Trade-off] `SaldoDisponivelApos` duplica um dado derivável.** → Aceito pela idempotência exata e pela auditoria. Ele é gravado uma única vez e nunca alterado.

## Descobertas da implementação

- **MassTransit 8.5.12 com EF Core 10:** funciona. Restore, build e os testes do Outbox (gravação atômica e entrega após a queda do broker) passaram; o plano B (EF Core 9) não foi necessário.
- **`valor` nos eventos trafega como texto decimal** (`"150.00"`), o padrão do serializador do MassTransit (`StringDecimalJsonConverter`). Decidido com o autor: nenhum consumidor converte para ponto flutuante. O contrato no `DOMINIO.md`, seção 7, foi atualizado. O `Valor` sempre tem duas casas (`30` vira `30.00`), para que resposta HTTP, banco e evento coincidam.
- **Instantes normalizados** para UTC truncado em microssegundos (`Instantes.Normalizar`), a precisão do PostgreSQL. Sem isso, a repetição idempotente devolveria uma `dataRegistro` diferente na última casa.
- **Corpo malformado em `Development`** vira `BadHttpRequestException`; o exception handler preserva o `400` dela (`StatusCodeSelector`), em vez de transformá-lo em `500`.
- **Testes que observam o Outbox usam banco próprio.** Com o banco compartilhado, o Delivery Service de outra instância da API nos testes entregava as mensagens ao broker errado.

## Migration Plan

Não há dados existentes, porque é a primeira migração. Para reverter, basta voltar o código: a change `estruturar-solucao` não usa as tabelas.

## Open Questions

Nenhuma. A compatibilidade do MassTransit com o EF Core 10 é verificada pela primeira tarefa e já tem plano B definido.
