# Proposal

## Why

A arquitetura e o domínio estão documentados em [DOMINIO.md](../../../../DOMINIO.md) e [ARQUITETURA.md](../../../../ARQUITETURA.md), mas ainda não existe código. Antes de implementar regras de negócio, precisamos de uma base que já atenda aos requisitos obrigatórios do enunciado que não dependem do domínio:

- código em C#;
- compilação sem erros e sem warnings;
- testes automatizados executáveis;
- README com instruções para rodar localmente.

Começar pelo esqueleto fixa a estrutura dos microserviços, as regras de dependência do DDD e o ambiente local. As changes seguintes, uma por fatia, só adicionam comportamento.

## What Changes

- Solução .NET 10 (LTS) com um conjunto de projetos por Bounded Context:
  - **Lançamentos:** Domínio, Aplicação, Infraestrutura e Api;
  - **Posição Consolidada:** Domínio, Aplicação, Infraestrutura, Api e Worker;
  - um **API Gateway** (YARP);
  - um projeto de **contratos de integração** (Published Language), ainda sem eventos;
  - uma **biblioteca técnica de hospedagem** com a configuração de saúde e de correlação comum aos serviços. Ela não tem conceitos de domínio, portanto não é um Shared Kernel.
- Configuração de build comum à solução: warnings tratados como erro, nullable habilitado, analisadores e versões de pacotes centralizadas. O MassTransit fica travado na faixa 8.x.
- Endpoints de saúde (`/health/live` e `/health/ready`) em todos os serviços. A prontidão verifica as dependências de cada um: PostgreSQL, RabbitMQ e Redis.
- Gateway roteando `/lancamentos/*` e `/posicao/*` para os serviços.
- `docker compose` com toda a infraestrutura e os serviços:
  - PostgreSQL por Bounded Context;
  - RabbitMQ;
  - Redis;
  - Dockerfile por serviço.
- Testes:
  - de arquitetura, garantindo que o Domínio não depende de nenhuma outra camada nem de frameworks e que os Bounded Contexts não se referenciam;
  - de saúde dos serviços;
  - de roteamento do gateway.
- Pipeline de CI (GitHub Actions) que compila e executa os testes.
- README na raiz com pré-requisitos, como subir o ambiente, como rodar os testes e um mapa da documentação.

**Fora do escopo** (próximas changes):
- regras de negócio, Aggregates e persistência do domínio;
- Outbox e consumers;
- autenticação JWT e verificação de titularidade;
- rate limiting, cache e observabilidade com OpenTelemetry;
- criação e publicação do repositório no GitHub, que o próprio autor faz.

## Capabilities

### New Capabilities

- `qualidade-de-build`: regras que a solução inteira respeita ao compilar e testar. Inclui zero warnings, versão do SDK fixa, dependências entre camadas e entre Bounded Contexts verificadas por testes, e CI.
- `saude-dos-servicos`: como cada serviço informa que está vivo e que está pronto para receber tráfego, incluindo o estado das suas dependências.
- `api-gateway`: ponto único de entrada que encaminha as requisições externas para o serviço do Bounded Context correspondente.
- `ambiente-local`: como a solução completa é executada localmente com um único comando e documentada no README.

### Modified Capabilities

Nenhuma. O projeto ainda não tem specs.

## Impact

- **Código novo:** `src/`, `tests/`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`, `docker-compose.yml`, `Dockerfile`s, `.github/workflows/ci.yml` e `README.md`.
- **Dependências novas:**
  - YARP;
  - MassTransit 8.x com transporte RabbitMQ;
  - Npgsql (só para a verificação de saúde; o EF Core entra na change que implementar a persistência);
  - StackExchange.Redis;
  - pacotes de health checks;
  - xUnit, NetArchTest e Testcontainers.
- **Ferramentas exigidas de quem for rodar:** .NET SDK 10 e Docker.
- **Documentação:** o README passa a ser o ponto de entrada do repositório e aponta para `DOMINIO.md` e `ARQUITETURA.md`.
