# Tasks

## 1. Dependências e compatibilidade

- [x] 1.1 Adicionar ao `Directory.Packages.props`:
  - EF Core 10 (`Microsoft.EntityFrameworkCore.Design`) e `Npgsql.EntityFrameworkCore.PostgreSQL` 10;
  - `MassTransit.EntityFrameworkCore` na faixa `[8.5.12,9.0)`;
  - `Microsoft.AspNetCore.Authentication.JwtBearer` 10 e `Microsoft.IdentityModel.JsonWebTokens`;
  - `RabbitMQ.Client`.

  Referenciar os pacotes em `Lancamentos.Infraestrutura` e `Lancamentos.Api`. Verificar que `dotnet build -c Release` termina com 0 warnings e que o EF Core resolvido é o 10. Se o MassTransit 8 exigir outra versão, aplicar o plano B do design (EF Core 9) e registrar a escolha no design.
- [x] 1.2 Criar `.config/dotnet-tools.json` com o `dotnet-ef` na versão do EF Core resolvido; verificar com `dotnet tool restore` e `dotnet ef --version`
- [x] 1.3 Criar o projeto `tests/Lancamentos.Unidade.Testes`, que referencia `Lancamentos.Aplicacao`, e adicioná-lo à solução; verificar que `dotnet test` o executa

## 2. Domínio de Lançamentos

- [x] 2.1 Implementar `Resultado<T>`, `Falha` e `CodigosDeFalha`, e os Value Objects `Valor`, `ContaId`, `LancamentoId`, `TitularId`, `ChaveIdempotencia` e `Descricao`, com as regras de D1; verificar com testes unitários dos limites de cada um (por exemplo, `10.005` recusado, `10.500` aceito, `999999999999.99` aceito, 201 caracteres recusados)
- [x] 2.2 Implementar o Aggregate `Conta` (`Abrir`, `PertenceA`, `RegistrarCredito`, `RegistrarDebito`, com situação, limite, sequência e `Versao`) e a Entity imutável `Lancamento` com `SaldoDisponivelApos`. Verificar com testes unitários:
  - conta inativa;
  - saldo insuficiente, inclusive com débito exato do saldo;
  - sequência sem lacunas após recusa;
  - data de competência no futuro, no dia anterior e omitida;
  - eventos de domínio gerados.
- [x] 2.3 Implementar `Conta.Estornar(original, jaEstornado, ...)`, com o efeito inverso, as invariantes de saldo, o estorno único e o estorno de estorno recusado; verificar com testes unitários dos cenários da spec `lancamentos`
- [x] 2.4 Atualizar o `DOMINIO.md` e o diagrama 2.1 com:
  - `SaldoDisponivelApos`;
  - `ChaveAbertura`;
  - o limite inicial zero;
  - a idempotência da abertura;
  - os códigos de erro (5.4);
  - a decisão de bloqueio da linha (seção 8).

  Verificar que os diagramas renderizam e que os links funcionam.

## 3. Contratos de integração e regras de arquitetura

- [x] 3.1 Criar `ContaAbertaV1` e `MovimentacaoRegistradaV1` em `Contratos.Integracao/Lancamentos`, com os campos do `DOMINIO.md`, seção 7; verificar com um teste de serialização JSON que os nomes dos campos batem com o contrato documentado e que `MovimentacaoRegistradaV1` não tem `titularId` nem descrição
- [x] 3.2 Atualizar `Arquitetura.Testes`:
  - a Infraestrutura passa a poder referenciar `Contratos.Integracao`;
  - nova regra: Domínio e Aplicação sem dependência dos contratos.

  Verificar que os testes passam e que falham ao adicionar temporariamente uma referência de `Lancamentos.Aplicacao` para `Contratos.Integracao`.

## 4. Casos de uso

- [x] 4.1 Definir as portas (`IRepositorioDeContas`, `IRepositorioDeLancamentos`, `IUnidadeDeTrabalho`, `IContextoDaRequisicao`) e implementar `AbrirConta`, `RegistrarCredito`, `RegistrarDebito` e `EstornarLancamento`, na ordem de D2. Verificar em `Lancamentos.Unidade.Testes`, com portas falsas:
  - a titularidade é checada antes da chave;
  - a repetição devolve o lançamento original;
  - a mesma chave com outro conteúdo dá `chave-idempotencia-conflitante`;
  - a `dataCompetencia` omitida não entra na comparação;
  - uma operação recusada não é gravada.

## 5. Persistência e Outbox

- [x] 5.1 Implementar o `LancamentosDbContext` com o mapeamento `snake_case`, `numeric(14,2)`, `Versao` como token de concorrência, os quatro índices únicos de D4 e as entidades do Outbox do MassTransit. Gerar a migração inicial em `Migracoes/`. Verificar com `dotnet ef migrations script` que o SQL contém os índices únicos e as tabelas do Outbox.
- [x] 5.2 Implementar os repositórios (carga com `SELECT ... FOR UPDATE`), a unidade de trabalho com transação e a tradução das violações `23505` para as falhas de negócio pelo nome do índice. Verificar com testes de integração (Testcontainers) que:
  - inserir uma sequência ou chave duplicada dá a falha correta;
  - um segundo estorno do mesmo lançamento é barrado pelo índice.
- [x] 5.3 Implementar o `TradutorDeEventos` (Domain Event → `ContaAbertaV1`/`MovimentacaoRegistradaV1`, com `MessageId = EventoId` e `correlacaoId`) e configurar `AddEntityFrameworkOutbox` com `UsePostgres` e `UseBusOutbox`. Verificar com um teste de integração que uma operação confirmada deixa exatamente uma linha em `OutboxMessage` e que uma transação desfeita não deixa nenhuma.
- [x] 5.4 Implementar o `AplicadorDeMigracoes` (`BackgroundService` com nova tentativa a cada 5 s, ativado por `Banco:AplicarMigracoesNaSubida`) e a verificação de saúde `migracoes`. Verificar com teste de integração:
  - com o banco parado na subida, o processo continua vivo e `ready = 503`, com `migracoes = Unhealthy`;
  - ao iniciar o banco, fica `200` sem reiniciar.

## 6. Autenticação

- [x] 6.1 Implementar em `Plataforma.Hospedagem` o `AdicionarAutenticacaoJwt` (por `Autoridade` ou `ChaveDeAssinatura`, mais `Emissor` e `Audiencia`, com `MapInboundClaims = false` e validação do `sub`) e a política `ExigirEscopo`. Verificar com testes em `Plataforma.Hospedagem.Testes` que dão `401`:
  - sem token;
  - com assinatura errada;
  - com token expirado;
  - com outro emissor ou outra audiência;
  - sem `sub` ou com `sub` maior que 200 caracteres.

  E que dão `403` sem o escopo e `200` com vários escopos.
- [x] 6.2 Criar `ferramentas/EmissorDeTokens` (console, `--titular`, `--escopos`, `--validade-minutos`, configuração por `Autenticacao__*`), com Dockerfile, e adicioná-lo à solução; verificar que um token emitido por ele é aceito por um teste que usa a mesma chave

## 7. API de Lançamentos

- [x] 7.1 Configurar na Lancamentos.Api:
  - a autenticação;
  - o grupo `/lancamentos` com o escopo `lancamentos:escrever`;
  - `AddProblemDetails`, `UseStatusCodePages` e `UseExceptionHandler`;
  - `UsarCorrelacao`;
  - o conversor de `dataCompetencia`, que exige o fuso;
  - o mapeamento de falha para status de D7.

  Verificar com testes que:
  - a saúde continua anônima;
  - JSON malformado e data sem fuso dão `400 requisicao-invalida` em `application/problem+json`;
  - um erro forçado dá `500` sem stack trace.
- [x] 7.2 Implementar `POST /lancamentos/contas`; verificar com testes de integração os cenários das specs `contas` e `autenticacao`:
  - a abertura, com limite ignorado;
  - repetição e chave ausente;
  - a mesma chave usada por titulares diferentes;
  - os ids não sequenciais;
  - `titularId` no corpo ignorado;
  - `401`/`403`.
- [x] 7.3 Implementar `POST .../creditos` e `POST .../debitos`; verificar com testes de integração os cenários da spec `lancamentos`:
  - valores, descrição e data de competência;
  - saldo, inclusive o débito exato;
  - conta bloqueada, via banco;
  - sequência;
  - idempotência: repetição, conteúdo conflitante e repetição de operação recusada;
  - os cenários da spec `titularidade`.
- [x] 7.4 Implementar `POST .../lancamentos/{lancamentoId}/estornos`; verificar com testes de integração:
  - estorno de débito;
  - estorno de crédito sem saldo;
  - segundo estorno com outra chave (`409`);
  - estorno de estorno;
  - lançamento de outra conta (`404`);
  - `DELETE`/`PUT`/`PATCH` sem efeito.
- [x] 7.5 Implementar o teste de concorrência: 10 débitos simultâneos de `10.00` numa conta com `50.00` resultam em exatamente 5 `201` e 5 `422`, com saldo final `0.00` e sequências contíguas. Executar 5 vezes seguidas, sem falhas intermitentes.

## 8. Eventos de ponta a ponta

- [x] 8.1 Com RabbitMQ real e um consumidor de teste via `RabbitMQ.Client`, verificar:
  - a abertura publica um `ContaAberta` v1 com `sequencia = 0` e `titularId`;
  - a repetição da abertura não publica outro;
  - débito e estorno publicam `MovimentacaoRegistrada` v1 com os campos e `tipo` corretos;
  - operações recusadas não publicam nada;
  - `MovimentacaoRegistrada` não contém `titularId` nem descrição.
- [x] 8.2 Verificar a tolerância ao broker: com o RabbitMQ parado (porta fixa), um crédito responde `201` e fica em `OutboxMessage`; ao iniciar o RabbitMQ, o evento é entregue sem intervenção, com `MessageId` igual ao `eventoId`

## 9. Ambiente local e documentação

- [x] 9.1 Atualizar o ambiente local:
  - criar `ambiente/desenvolvimento/autenticacao.env`, com chave, emissor e audiência de desenvolvimento;
  - entregar esse arquivo à `lancamentos-api`, junto com `Banco__AplicarMigracoesNaSubida=true`;
  - adicionar o serviço `emissor-de-tokens` no profile `ferramentas`.

  Verificar que `docker compose up -d --build` deixa tudo `healthy` com o volume vazio e que `docker compose run --rm emissor-de-tokens --titular cliente-1` emite um token aceito em `POST http://localhost:${GATEWAY_PORTA}/lancamentos/contas`.
- [x] 9.2 Atualizar o README com:
  - uma seção "Usando a API": emitir token, abrir conta, creditar, debitar e estornar com `curl`;
  - os códigos de erro;
  - o aviso sobre a chave de desenvolvimento;
  - as migrações em produção.

  Verificar executando os comandos do README, como escritos, no ambiente local.
- [x] 9.3 Atualizar o diagrama 2.2 (Tradutor de Eventos na Infraestrutura, Autenticação JWT na borda), o diagrama 2.5 (bloqueio da conta) e o `ARQUITETURA.md`, seção 3.1 (concorrência por bloqueio da linha); verificar que os diagramas renderizam sem erro e que os links funcionam

## 10. Verificação final

- [x] 10.1 Numa cópia limpa do repositório:
  - rodar `dotnet build -c Release`, com 0 warnings;
  - rodar `dotnet test -c Release`, com todos passando;
  - rodar `openspec validate --strict`;
  - seguir a seção "Usando a API" do README no compose.

  Registrar o resultado nesta tarefa.

  - Resultado (cópia limpa, 2026-10-28):
    - build com 0 warnings e 0 erros;
    - 182 testes passando em 6 projetos (Unidade 57, Arquitetura 23, Gateway 17, Plataforma 17, Posição 5, Lançamentos.Api 63);
    - `openspec validate --all --strict` com 5 de 5 válidos;
    - compose com os 8 containers `healthy` e o volume vazio;
    - "Usando a API" seguido como escrito: crédito de 100, débito de 30 e estorno, com saldo final de 100.

    A porta 8080 estava ocupada na máquina, então a verificação usou `GATEWAY_PORTA=8090`.

## Workflow follow-up

- Arquivar a change (`/opsx:archive`) após a revisão, promovendo as specs novas e as modificações em `saude-dos-servicos`, `ambiente-local` e `qualidade-de-build`.
- Próxima change sugerida: BC Posição Consolidada (consumers com Inbox, `Titularidade`, `FechamentoDiario`, `Movimento` e consultas), já usando os contratos e a autenticação desta change.
