# Design

## Context

O repositório ainda não tem código: só a documentação ([DOMINIO.md](../../../../DOMINIO.md), [ARQUITETURA.md](../../../../ARQUITETURA.md) e `diagramas/`), um repositório git sem commits e o OpenSpec. A máquina de desenvolvimento tem os SDKs .NET 10 e o Docker 29.

A arquitetura já está decidida:
- um microserviço por Bounded Context;
- MassTransit 8.x sobre RabbitMQ;
- PostgreSQL por contexto;
- Redis na Posição Consolidada;
- gateway YARP;
- consumer de Posição num `Posicao.Worker` separado da API.

Este design trata apenas de **como montar o esqueleto**. A motivação está em [proposal.md](proposal.md) e os comportamentos exigidos estão nas specs.

## Goals / Non-Goals

**Goals:**
- Fixar a estrutura de pastas, de projetos e de dependências que as próximas changes vão preencher.
- Fazer as fronteiras do DDD falharem no teste, e não só na revisão de código.
- Deixar `docker compose up` e `dotnet test` funcionando desde o primeiro commit.

**Non-Goals:**
- Nenhum endpoint de negócio, entidade, migration ou fila de domínio.
- Nenhuma configuração de produção (Kubernetes, TLS interno, cofre de segredos). Ficam documentadas em `ARQUITETURA.md`.
- Nenhuma autenticação: o gateway encaminha sem validar token nesta change.

## Decisions

### D1. Estrutura de pastas e projetos

```
DesafioTecnico.slnx
global.json  Directory.Build.props  Directory.Packages.props  .editorconfig
docker-compose.yml
src/
  Lancamentos/   Lancamentos.Dominio  Lancamentos.Aplicacao  Lancamentos.Infraestrutura  Lancamentos.Api
  Posicao/       Posicao.Dominio  Posicao.Aplicacao  Posicao.Infraestrutura  Posicao.Api  Posicao.Worker
  Gateway/       Gateway
  Compartilhado/ Contratos.Integracao  Plataforma.Hospedagem
tests/
  Arquitetura.Testes  Plataforma.Hospedagem.Testes  Lancamentos.Api.Testes  Posicao.Testes  Gateway.Testes
ambiente/desenvolvimento/   um .env por container
```

- Usamos nomes de projeto em português, sem acento, para seguir a convenção do `DOMINIO.md`.
- Usamos `.slnx`, o formato de solução em XML suportado nativamente pelo SDK 10, mais legível e com menos conflitos de merge que o `.sln`.
- **Alternativa descartada:** um repositório por serviço. Ele seria mais fiel à independência de deploy, mas o enunciado pede um repositório público e uma revisão única. A independência é garantida pelos testes de arquitetura (D6) e por um Dockerfile por serviço.

### D2. Regras de build centralizadas

`Directory.Build.props` na raiz aplica a todos os projetos:
- `net10.0`;
- `C# 14`;
- `Nullable` e `ImplicitUsings` habilitados;
- `TreatWarningsAsErrors=true`;
- `AnalysisMode=Recommended`;
- `EnforceCodeStyleInBuild=true`.

O `.editorconfig` ajusta as regras de analisador barulhentas. Toda regra rebaixada leva um comentário com o motivo, para que rebaixar uma regra seja uma decisão visível e não um atalho.

`global.json` declara `10.0.100` com `rollForward: latestFeature`. Isso aceita qualquer SDK 10.0.x e impede que outra versão principal instalada ao lado seja usada. O .NET 10 é a versão LTS atual.

### D3. Pacotes centralizados e MassTransit travado na v8

`Directory.Packages.props` usa Central Package Management. O MassTransit é declarado como faixa `[8.x.y,9.0)`, com `8.x.y` sendo a versão 8 mais recente no momento da implementação:
- O NuGet resolve sempre o limite inferior, então a restauração é determinística.
- As ferramentas de atualização respeitam o teto e nunca sobem para a v9.

Um teste em `Arquitetura.Testes` confere que a versão principal do assembly `MassTransit` carregado é 8. Ele cobre a spec mesmo se alguém editar a faixa.

- **Alternativa descartada:** versão exata sem faixa. Ela é igualmente determinística, mas um `dotnet outdated -u` levaria para a v9 sem aviso.

### D4. Biblioteca técnica `Plataforma.Hospedagem`

Concentra o que os quatro serviços configuram de forma idêntica:
- registro de `/health/live` e `/health/ready`;
- o escritor JSON da prontidão, que expõe apenas o nome lógico e o estado de cada dependência;
- o middleware de `X-Correlation-Id`.

- **Por que não é um Shared Kernel:** não contém nenhum tipo do domínio. É o equivalente a um pacote NuGet interno. Testes de arquitetura garantem que ela não referencia nenhum Bounded Context e que só os projetos de borda (Api, Worker, Gateway) a usam.
- **Alternativa descartada:** duplicar cerca de 60 linhas em cada serviço. Mantém a independência total, mas quatro cópias de uma regra de segurança (não vazar exceções na saúde) tendem a divergir.

### D5. Saúde dos serviços

- A vida (`/health/live`) usa um predicado que não executa nenhuma verificação. A prontidão (`/health/ready`) executa só as verificações marcadas com a tag `ready`.
- As verificações são estas:
  - **PostgreSQL:** `AspNetCore.HealthChecks.NpgSql`.
  - **Redis:** `AspNetCore.HealthChecks.Redis`, com `failureStatus: Degraded`. Por isso o cache fora do ar devolve `200`, que é o mapeamento padrão de `Degraded`.
  - **RabbitMQ:** a verificação nativa do MassTransit. Na Lancamentos.Api e no Posicao.Worker o bus já é configurado agora, sem consumers nem publicações. A change do Outbox só adiciona a configuração em cima.
- O escritor JSON ignora `exception` e `description` das entradas, o que garante que nenhum detalhe interno vaze.
- **Posicao.Worker como host web:** usa `WebApplication` e expõe apenas os endpoints de saúde, sem rotas de negócio.
  - **Alternativa descartada:** um Worker Service puro com sonda por arquivo ou TCP. O orquestrador e o compose ficariam com sondas diferentes por serviço, e não seria possível ver as dependências no corpo da resposta.
- **Inicialização tolerante:** nenhum serviço testa conexão no `Main`. O MassTransit reconecta ao broker em segundo plano, e as verificações de PostgreSQL e Redis abrem a conexão a cada chamada. O processo nunca encerra por dependência ausente.

### D6. Testes

| Projeto | O que cobre | Como |
|---|---|---|
| `Arquitetura.Testes` | Specs de dependências e versão do MassTransit | NetArchTest sobre os tipos de cada assembly, mais a leitura dos `ProjectReference`/`PackageReference` dos `.csproj`. As referências são lidas do `.csproj` porque o compilador descarta do assembly as referências sem uso de tipos |
| `Plataforma.Hospedagem.Testes` | Escritor da saúde sem vazamento de detalhes | Host de teste com verificações falsas que falham com host e senha na mensagem |
| `Lancamentos.Api.Testes`, `Posicao.Testes` | Specs de saúde | `WebApplicationFactory`. Os cenários de "dependências acessíveis" usam Testcontainers (PostgreSQL, RabbitMQ, Redis); os de falha apontam para portas fechadas e conferem `503` e a ausência de detalhes no corpo |
| `Gateway.Testes` | Specs do gateway | O gateway em `WebApplicationFactory` encaminha para dois backends falsos (Kestrel em porta aleatória) que registram as requisições recebidas |

- Usamos xUnit com as asserções nativas.
- **Alternativa descartada:** FluentAssertions, porque a v8 passou a ter licença comercial. É a mesma lógica aplicada ao MassTransit.
- Os testes com Testcontainers exigem Docker, que já é pré-requisito do ambiente local e existe nos runners do GitHub.

### D7. Gateway

- As rotas YARP ficam em `appsettings.json`: `/lancamentos/{**resto}` e `/posicao/{**resto}`. O caminho é **preservado**, sem remover o prefixo, e cada serviço publica seus endpoints de negócio sob o próprio prefixo.
  - Com isso, a URL de um recurso é a mesma dentro e fora do gateway, e `/health/*` dos serviços nunca coincide com uma rota pública.
- Um middleware antes do proxy responde `404` para `/{lancamentos|posicao}/health/**`. Isso é defesa explícita, caso alguém mude a regra de prefixo.
- O `ActivityTimeout` de cada cluster é de 10 s, configurável. Um destino indisponível devolve `502` (conexão recusada) ou `504` (tempo esgotado), que é o comportamento padrão do YARP.
- O middleware de correlação de `Plataforma.Hospedagem`:
  - aceita o valor recebido se ele casar com `^[A-Za-z0-9-]{1,64}$`;
  - se não casar, gera um GUID;
  - propaga o valor no cabeçalho encaminhado e na resposta.

### D8. Ambiente local

- O `docker-compose.yml` tem os seguintes serviços:
  - `postgres-lancamentos` e `postgres-posicao` (`postgres:16-alpine`);
  - `rabbitmq` (`rabbitmq:3.13-management-alpine`);
  - `redis` (`redis:7-alpine`);
  - `lancamentos-api`, `posicao-api`, `posicao-worker` e `gateway`.
- Só duas portas são publicadas no host: `gateway` em `${GATEWAY_PORTA:-8080}` e o painel do RabbitMQ em `15672`. A porta do gateway é configurável porque a 8080 costuma estar ocupada (Keycloak, por exemplo).
- **Credenciais:**
  - Ficam versionadas em `ambiente/desenvolvimento/`, **um arquivo por container**, com valores falsos e um cabeçalho dizendo que são exclusivos para uso local, carregados por `env_file`.
  - Cada serviço recebe só a connection string do próprio banco. Um único `desenvolvimento.env` (o plano inicial) entregaria todas as credenciais a todos os containers e violaria a spec; a contrapartida é repetir a senha de cada banco no arquivo do banco e no do serviço.
  - Os `appsettings*.json` dos serviços não têm credenciais.
  - **Alternativa descartada:** `.env.example` que precisa ser copiado. Acrescentaria um passo e quebraria a "subida com um comando".
- **Dockerfiles:**
  - Há um por serviço, em multi-stage, com o contexto de build na raiz, porque precisam dos `Directory.*.props`.
  - A execução usa o usuário não-root `app` das imagens .NET 10 (`mcr.microsoft.com/dotnet/aspnet:10.0`, compilando com `sdk:10.0`).
  - O runtime instala `curl` para o `healthcheck` do compose chamar `/health/ready`. Assim `docker compose ps` mostra `healthy` sem expor as portas dos serviços.
- `depends_on` com `condition: service_healthy` só para a infraestrutura. É uma conveniência, porque os serviços já toleram a infraestrutura atrasada (D5).

### D9. CI

`.github/workflows/ci.yml` roda em `push` e `pull_request`:
1. `actions/setup-dotnet`, lendo o `global.json`;
2. `dotnet build -c Release`, onde os warnings já falham pelo `Directory.Build.props`;
3. `dotnet test -c Release --no-build`;
4. `docker compose config`, para validar o compose.

## Risks / Trade-offs

- **[Risco] `AnalysisMode=Recommended` gera uma enxurrada de erros em código gerado ou de teste.** → Mitigação: ajustes no `.editorconfig` restritos às pastas `tests/`, sempre com motivo. Se uma regra for desligada para toda a solução, ela precisa constar no design da change que a desligar.
- **[Risco] Testes com Testcontainers ficam lentos ou instáveis no CI.** → Mitigação: um container por tipo, compartilhado por classe de teste (`IClassFixture`), imagens fixas por tag e espera pela prontidão do container antes do teste.
- **[Risco] O MassTransit pode encerrar o host se o broker estiver fora na partida, dependendo da configuração de espera do bus.** → Mitigação: manter a espera pela partida desligada (padrão) e cobrir com um teste que inicia a Lancamentos.Api sem broker e confere `live = 200` e `ready = 503`.
- **[Trade-off] Monorepo com uma solução só.** Ele facilita revisar e rodar, mas permite referências entre contextos por engano. → Mitigação: os testes de arquitetura (D6) quebram o build nesse caso.
- **[Trade-off] Biblioteca técnica compartilhada (D4).** Uma mudança nela afeta os quatro serviços de uma vez. É aceitável enquanto ela ficar restrita a hospedagem. Se começar a crescer com regras, deve ser dividida ou virar pacote versionado.
- **[Trade-off] `curl` na imagem de runtime.** Aumenta um pouco a imagem e a superfície de ataque, mas permite healthcheck sem expor portas. Em produção, a sonda do orquestrador faz HTTP direto e o `curl` pode sair.

## Open Questions

- A versão 8.x exata do MassTransit e as versões dos demais pacotes serão as mais recentes estáveis no momento da implementação. Isso não altera specs nem tarefas.
