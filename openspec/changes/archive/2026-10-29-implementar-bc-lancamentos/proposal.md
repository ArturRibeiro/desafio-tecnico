# Proposal

## Why

O esqueleto da solução está pronto, mas o sistema ainda não registra nenhuma movimentação. Lançamentos é o Core Subdomain: é onde está o risco de dinheiro errado na conta do cliente, e é a fonte da verdade da qual a Posição Consolidada depende. Por isso é a primeira fatia de comportamento a ser implementada. A modelagem já está decidida em [DOMINIO.md](../../../../DOMINIO.md), seções 5 e 7.

## What Changes

- **Domínio de Lançamentos:**
  - Aggregate Root `Conta`, Entity `Lancamento` e Value Objects (`Valor`, `ContaId`, `LancamentoId`, `TitularId`, `ChaveIdempotencia`, `Descricao`);
  - as cinco invariantes da `Conta`, as regras de estorno e de data de competência e a verificação de titularidade (`PertenceA`);
  - falhas de negócio como `Resultado`, e não como exceções.
- **Casos de uso:** `AbrirConta`, `RegistrarCredito`, `RegistrarDebito` e `EstornarLancamento`, todos idempotentes pela chave enviada pelo cliente e todos verificando a titularidade antes de qualquer outra regra.
- **API HTTP sob `/lancamentos`:**
  - abrir conta;
  - registrar crédito;
  - registrar débito;
  - estornar lançamento.

  Os erros seguem o formato Problem Details, com um código estável por falha de negócio.
- **Autenticação JWT na Lancamentos.Api:**
  - validação do token;
  - escopo `lancamentos:escrever`;
  - a claim `sub` usada como `TitularId`.

  Para o ambiente local, um **emissor de tokens de desenvolvimento**.
- **Persistência em PostgreSQL** com EF Core:
  - bloqueio da linha da `Conta` durante cada operação, com concorrência otimista (`Versao`) como proteção extra;
  - restrições únicas que reforçam no banco as regras de sequência, idempotência e estorno único;
  - migrações aplicadas automaticamente na subida, sem impedir a inicialização tolerante.
- **Eventos de integração:**
  - `ContaAberta v1` e `MovimentacaoRegistrada v1` no projeto `Contratos.Integracao`;
  - gravados no Transactional Outbox do MassTransit, na mesma transação do lançamento;
  - entregues ao RabbitMQ pela própria Lancamentos.Api.
- A prontidão da Lancamentos.Api passa a considerar também o esquema do banco.
- `DOMINIO.md` é atualizado com as decisões tomadas aqui:
  - idempotência da abertura de conta;
  - limite inicial zero;
  - códigos de erro.

**Fora do escopo:**
- consultas de saldo ou de extrato, que são responsabilidade de Posição Consolidada;
- alteração de limite;
- bloqueio e encerramento de conta;
- autenticação na Posicao.Api e no gateway;
- integração com um Provedor de Identidade real.

## Capabilities

### New Capabilities

- `autenticacao`: como um cliente prova quem é ao chamar o sistema, quais permissões o token carrega e como o sistema identifica o titular.
- `titularidade`: a garantia de que um cliente só opera as contas de que é titular, sem revelar a existência de contas alheias.
- `contas`: abertura de conta no contexto de Lançamentos, com o titular, a situação inicial e o limite inicial.
- `lancamentos`: registro de créditos, débitos e estornos, com as regras de saldo, limite, data de competência, sequência, idempotência e concorrência.
- `eventos-de-integracao`: os eventos que Lançamentos publica para outros contextos (Published Language), com o contrato e as garantias de entrega.

### Modified Capabilities

- `saude-dos-servicos`: a prontidão da Lancamentos.Api passa a incluir o esquema do banco, além do PostgreSQL e do RabbitMQ.
- `ambiente-local`: o ambiente local passa a oferecer tokens de desenvolvimento e a preparar o banco de Lançamentos sozinho.
- `qualidade-de-build`: a Infraestrutura passa a poder referenciar os contratos de integração, onde fica a tradução de Domain Events em Integration Events. Domínio e Aplicação continuam proibidos de conhecê-los.

## Impact

- **Código:**
  - `src/Lancamentos/*`, nas quatro camadas;
  - `src/Compartilhado/Contratos.Integracao`, com os dois eventos;
  - `src/Compartilhado/Plataforma.Hospedagem`, com a configuração de autenticação reutilizável pelos outros serviços;
  - nova ferramenta `ferramentas/EmissorDeTokens`.
- **Testes:**
  - novo projeto `Lancamentos.Unidade.Testes`, para o Domínio e os casos de uso;
  - `Lancamentos.Api.Testes` ganha os testes da API, da persistência, da concorrência e do Outbox, com PostgreSQL e RabbitMQ reais.
- **Dependências novas:**
  - EF Core 10 com o provider Npgsql;
  - `MassTransit.EntityFrameworkCore` (8.x);
  - `Microsoft.AspNetCore.Authentication.JwtBearer`;
  - `Microsoft.IdentityModel.JsonWebTokens`, no emissor de desenvolvimento;
  - `RabbitMQ.Client`, só nos testes.
- **API pública:** novas rotas `POST` sob `/lancamentos`, acessíveis pelo gateway, que hoje só repassa o cabeçalho `Authorization`.
- **Ambiente local:** novo arquivo de credenciais de autenticação em `ambiente/desenvolvimento/` e um serviço de ferramenta no compose.
