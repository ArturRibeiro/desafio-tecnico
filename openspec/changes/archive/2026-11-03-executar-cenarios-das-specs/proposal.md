# Proposal

## Why

- Os projetos de teste de integração foram removidos, e restaram só testes de unidade e de arquitetura.
- Os fluxos de negócio de ponta a ponta perderam a verificação automática. Isso inclui:
  - abrir conta, creditar, debitar e estornar;
  - a publicação dos eventos;
  - a projeção da posição;
  - a consulta da posição.
- As specs de negócio descrevem esses fluxos em cenários, mas nada garante que cada cenário escrito é executado e continua passando.

## What Changes

- Novo projeto de teste dedicado aos fluxos de negócio:
  - executa, como cenários Gherkin, todos os cenários das specs de negócio;
  - os serviços rodam no processo, com PostgreSQL, RabbitMQ e Redis reais em containers.
- As specs passam a ser classificadas como **de negócio** (com arquivo de cenários) ou **técnicas** (sem).
  - São de negócio: `contas`, `titularidade`, `lancamentos`, `eventos-de-integracao`, `projecao-da-posicao` e `consulta-de-posicao`.
  - As técnicas ficam fora desse projeto.
- Cada change que cria ou altera cenários de uma spec de negócio cria ou atualiza o arquivo de cenários dela.
- Um passo sem implementação faz o cenário falhar, e não ser ignorado.
- A convenção de nomes dos projetos de teste passa a aceitar o novo tipo.
- A documentação passa a apontar os cenários de negócio como evidência do que está implementado.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `qualidade-de-build`: passa a exigir que todo cenário das specs de negócio seja executado automaticamente, e que cada change mantenha os cenários executáveis das specs de negócio que alterar.

## Fora do escopo

- Specs técnicas (`autenticacao`, `api-gateway`, `saude-dos-servicos`, `observabilidade`, `ambiente-local` e `qualidade-de-build`): não entram nesse projeto, e a forma de verificá-las não muda nesta change.
- Mudar o comportamento do sistema: um cenário que falhar revela um defeito, que vira uma correção separada.
- Executar os cenários contra o ambiente do Docker Compose (caixa-preta) ou passando pelo API Gateway.
- Testes de carga.

## Impact

- **Código novo:** um projeto de teste e uma ferramenta de geração dos cenários.
- **Pacotes:** o framework Gherkin (SpecFlow com xUnit v2) e de volta os pacotes de containers e de host de teste.
- **Pré-requisito:** a suíte volta a precisar de Docker, já disponível no CI.
- **Tempo:** o tempo da suíte aumenta.
- **Documentação:** README, ARQUITETURA.md e as convenções do `openspec/config.yaml`.
