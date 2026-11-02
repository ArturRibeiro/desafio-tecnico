# Tasks

## 1. Base da solução e regras de build

- [x] 1.1 Criar `global.json` (10.0.100, `rollForward: latestFeature`), `.gitignore` e `.editorconfig`; verificar que `dotnet --version` na raiz retorna 10.0.x mesmo com o SDK 8 instalado
- [x] 1.2 Criar `Directory.Build.props` (net10.0, C# 14, Nullable, ImplicitUsings, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild`) e `Directory.Packages.props` com Central Package Management e MassTransit na faixa `[8.x.y,9.0)`; verificar que `dotnet restore` resolve o MassTransit 8.x
- [x] 1.3 Criar `DesafioTecnico.slnx` e os projetos de `src/` conforme D1 (Domínio, Aplicação, Infraestrutura, Api e Worker por contexto, mais Gateway, `Contratos.Integracao` e `Plataforma.Hospedagem`), com as referências entre projetos permitidas pelas specs; verificar que `dotnet build -c Release` termina com 0 warnings e 0 erros
- [x] 1.4 Criar os projetos de teste de `tests/` (xUnit, sem FluentAssertions) e adicioná-los à solução; verificar que `dotnet test` roda e informa os projetos executados

## 2. Testes de arquitetura

- [x] 2.1 Implementar em `Arquitetura.Testes` os testes de Domínio isolado e de dependências entre camadas, para os dois contextos; verificar que passam e que falham ao adicionar temporariamente uma referência de Domínio para Infraestrutura
- [x] 2.2 Implementar os testes de independência entre Bounded Contexts, de uso de `Plataforma.Hospedagem` restrito à borda e de `Contratos.Integracao` e `Plataforma.Hospedagem` sem dependências proibidas, checando as referências de assembly; verificar que falham ao adicionar temporariamente uma referência de `Posicao.Aplicacao` para `Lancamentos.Dominio`
- [x] 2.3 Implementar o teste que confere que a versão principal do assembly MassTransit é 8; verificar que passa
- [x] 2.4 Introduzir temporariamente um warning (variável não usada) num projeto qualquer; verificar que `dotnet build` falha e, depois, desfazer a alteração

## 3. Saúde dos serviços

- [x] 3.1 Implementar em `Plataforma.Hospedagem` o registro de `/health/live` (sem verificações) e de `/health/ready` (tag `ready`) e o escritor JSON que expõe só o nome lógico e o estado; verificar com teste unitário que o JSON não contém o texto da exceção de uma verificação que falha
- [x] 3.2 Configurar a Lancamentos.Api com as verificações `postgres` e `rabbitmq` (bus do MassTransit sem consumers) e testar em `Lancamentos.Api.Testes`:
  - com Testcontainers, `ready = 200`;
  - sem broker, `live = 200` e `ready = 503`, sem host nem credencial no corpo;
  - o processo não encerra ao iniciar sem broker.
- [x] 3.3 Configurar a Posicao.Api com `postgres` obrigatório e `redis` como `Degraded` e testar em `Posicao.Testes`:
  - com PostgreSQL e sem Redis, `ready = 200` e o `redis` aparece degradado;
  - sem PostgreSQL, `ready = 503`.
- [x] 3.4 Configurar o Posicao.Worker como host web só com os endpoints de saúde (`postgres` e `rabbitmq`) e testar em `Posicao.Testes` os casos de `ready` 200 e 503
- [x] 3.5 Garantir que nenhum `appsettings*.json` dos serviços contém credenciais (connection strings vêm de variáveis de ambiente); verificar com uma busca por `Password=` em `src/`, que deve voltar vazia

## 4. API Gateway

- [x] 4.1 Configurar as rotas YARP `/lancamentos/{**resto}` e `/posicao/{**resto}`, preservando o caminho, com `ActivityTimeout` de 10 s configurável, mais a saúde do próprio gateway; testar em `Gateway.Testes`, com dois backends falsos, o encaminhamento de método, caminho, query string, corpo e cabeçalhos
- [x] 4.2 Implementar o bloqueio `404` de `/{lancamentos|posicao}/health/**` e o `404` para caminhos sem rota; verificar com testes que os backends falsos não recebem essas requisições
- [x] 4.3 Testar o destino indisponível (`502`/`504` dentro do tempo limite) e o isolamento entre rotas (com Lançamentos fora, Posição continua respondendo)
- [x] 4.4 Implementar em `Plataforma.Hospedagem` o middleware de `X-Correlation-Id` (aceita `^[A-Za-z0-9-]{1,64}$`, gera um GUID caso contrário), usá-lo no gateway e testar os três cenários da spec (ausente, válido, inválido)

## 5. Ambiente local

- [x] 5.1 Criar um Dockerfile multi-stage por serviço (contexto na raiz, usuário `app`, `curl` no runtime); verificar que `docker build` de cada um termina com sucesso
- [x] 5.2 Criar `docker-compose.yml` e as credenciais por container em `ambiente/desenvolvimento/`, conforme D8:
  - dois PostgreSQL, RabbitMQ, Redis e os quatro serviços;
  - só as portas 8080 e 15672 publicadas;
  - `healthcheck` por serviço;
  - porta do gateway configurável (`GATEWAY_PORTA`).

  Verificar que `docker compose up -d --build` deixa todos os containers `healthy` em `docker compose ps` e que `docker compose port lancamentos-api 8080` não retorna porta publicada.
- [x] 5.3 Verificar a inicialização tolerante: subir a `lancamentos-api` com o `rabbitmq` parado, conferir que o container continua rodando e `unhealthy`, iniciar o `rabbitmq` e conferir que fica `healthy` sem reiniciar o serviço
- [x] 5.4 Escrever o `README.md` da raiz com:
  - pré-requisitos e versões;
  - subida com um comando;
  - build e testes;
  - como verificar a saúde (`docker compose ps` e `curl http://localhost:8080/health/ready`);
  - painel do RabbitMQ;
  - como parar e limpar (`docker compose down -v`);
  - links para `DOMINIO.md`, `ARQUITETURA.md` e `openspec/`.

  Verificar seguindo o README do zero, num clone limpo.

## 6. Integração contínua

- [x] 6.1 Criar `.github/workflows/ci.yml` (setup-dotnet via `global.json`, build Release, test, `docker compose config`); verificar a sintaxe com `docker compose config` e rodando localmente os mesmos comandos do workflow, na mesma ordem

## 7. Verificação final

- [x] 7.1 Num clone limpo, rodar `dotnet build -c Release` (0 warnings), `dotnet test` (todos passando) e `docker compose up -d --build` (todos `healthy`); registrar o resultado no resumo da change
  - Resultado (cópia limpa, 2026-10-28): build com 0 warnings e 0 erros; 49 testes passando em 5 projetos; `docker compose config` válido; os 8 containers `healthy` e `docker compose down -v` limpo. A porta 8080 estava ocupada na máquina, então a subida usou `GATEWAY_PORTA=8090`.

## Workflow follow-up

- Criar o repositório público no GitHub e fazer o push. Fica a cargo do autor, por ser uma publicação externa; só depois disso o CI roda de verdade.
- Arquivar a change (`/opsx:archive`) após a revisão, promovendo as specs para `openspec/specs/`.
- Próximas changes sugeridas: BC Lançamentos (domínio, persistência e Outbox), BC Posição Consolidada (consumers, projeções e consultas) e autenticação/titularidade no gateway e nos serviços.
