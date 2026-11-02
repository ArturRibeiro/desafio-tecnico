# Design

## Context

A motivação está em [proposal.md](proposal.md) e o comportamento exigido na spec `qualidade-de-build`.

Estado atual:

- **Testes:** restam `Lancamentos.Unit.Tests`, `Posicao.Unit.Tests`, `Plataforma.Mediator.Unit.Tests` e `Architecture.Tests`. Os projetos de integração foram apagados sem histórico, então o ambiente de teste (containers e fábricas dos serviços) precisa ser reescrito.
- **Specs de negócio:** 6 specs com 69 cenários.

  | Spec | Cenários |
  |---|---|
  | `contas` | 6 |
  | `titularidade` | 6 |
  | `lancamentos` | 24 |
  | `eventos-de-integracao` | 10 |
  | `projecao-da-posicao` | 11 |
  | `consulta-de-posicao` | 12 |

  Todas usam o mesmo formato: `#### Scenario:` e linhas `- **GIVEN/WHEN/THEN/AND**`.
- **Specs técnicas:** as outras 6 specs, com 77 cenários, ficam fora.
- **Serviços:** os fluxos de negócio passam pela Lancamentos.Api, pela Posicao.Api e pelo Posicao.Worker. O Gateway não faz parte do fluxo de negócio. Todos os serviços são configuráveis por `ConnectionStrings__*`, `Autenticacao__*` e `TimeProvider` injetado.
- **Convenção:** projetos de teste se chamam `<Area>.Unit.Tests` ou `Architecture.Tests`, e um teste de arquitetura confere isso.

## Goals / Non-Goals

**Goals:**

- Cada cenário de uma spec de negócio executado com o mesmo texto da spec, contra os serviços reais.
- Um passo sem implementação quebra a suíte.
- Cenários determinísticos: sem horário real e sem interferência entre cenários.

**Non-Goals:**

- Cenários das specs técnicas.
- Passar pelo API Gateway.
- Alterar specs ou serviços para facilitar os testes, além dos pontos de extensão que já existem.
- Paralelismo.

## Decisions

### D1. SpecFlow com xUnit v2

- **Pacotes:** `SpecFlow.xUnit` 3.9.74 (sobre o xUnit v2, porque o SpecFlow não suporta o xUnit v3) entra no `Directory.Packages.props`. As asserções são do FluentAssertions, as mesmas dos outros projetos de teste.
- **Execução pelo VSTest:** o projeto é uma biblioteca (os de xUnit v3 são executáveis), para que o `dotnet test` e o Rider descubram os testes.
- **Em série:** o `xunit.runner.json` desliga o paralelismo entre classes; cada `Feature` é uma classe, e os cenários compartilham os containers.
- **Sem arquivo de configuração:** não há `specflow.json`. Um passo sem binding ou pendente, que por padrão deixaria o cenário inconclusivo, falha o cenário por um `[AfterScenario]` do `Hooks`.
- **Código gerado:** o SpecFlow gera um `.feature.cs` ao lado de cada `.feature`. Ele fica fora do Git e das convenções do `Architecture.Tests`.
- **Risco aceito:** o SpecFlow foi descontinuado (fim do suporte em 31/12/2024) e não declara suporte a .NET 10.
- **FluentAssertions travado na v7** (Apache 2.0): a v8 exige licença comercial da Xceed.

*Alternativas descartadas:* `Reqnroll.xUnit.v3`, o sucessor mantido do SpecFlow, com `reqnroll.json` (`missingOrPendingStepsOutcome = Error`), e `SpecFlow.NUnit`. Foram trocados por decisão do projeto.

### D2. Classificação das specs

Os próprios `.feature` são a classificação: uma spec com `.feature` em `tests/Scenarios.Integration.Specs.Tests/Features` é de negócio, e uma spec sem `.feature` é técnica.

- **Classificação inicial:**
  - **negócio:** as 6 specs da tabela acima;
  - **técnica:** `autenticacao`, `api-gateway`, `saude-dos-servicos`, `observabilidade`, `ambiente-local` e `qualidade-de-build`.
- **Casos de fronteira:**
  - `eventos-de-integracao` é de negócio, porque descreve o fluxo de um lançamento até o outro contexto;
  - `autenticacao` é técnica, porque trata do token e dos escopos. A regra de negócio "só o titular opera a própria conta" está em `titularidade`.

*Alternativa descartada:* um arquivo de classificação (`classificacao-das-specs.json`) com as listas `negocio` e `tecnica`. Ele repetia o que os `.feature` já dizem.

### D3. `.feature` mantidos pelas changes

Cada change do OpenSpec que cria ou altera cenários de uma spec de negócio cria ou atualiza o `.feature` dessa spec e os passos que o executam. A regra está nas tarefas do `openspec/config.yaml`.

| Spec | Gherkin |
|---|---|
| nome da capability e `## Purpose` | `Feature:` e descrição |
| `### Requirement:` | `Rule:`, com o texto do requisito como descrição |
| `#### Scenario:` | `Scenario:` |
| `**GIVEN** / **WHEN** / **THEN** / **AND**` | `Given / When / Then / And`, com o texto sem alteração |

- **Nome do arquivo:** o da spec em PascalCase (`consulta-de-posicao` vira `ConsultaDePosicao.feature`).
- **Garantia:** a revisão da change confere o `.feature` contra a spec; um cenário sem passos implementados falha a suíte (D1).

*Alternativa descartada:* um gerador (`ferramentas/GeradorDeCenarios`) que convertia as specs em `.feature`, com um teste de sincronia e um relatório de rastreio. Era um projeto a mais só para essa conversão; o `.feature` passa a ser parte do trabalho da change, como o código e os testes.

### D5. Ambiente de execução

- **Ciclo (`Hooks`):** uma classe de hooks do SpecFlow concentra o ciclo:
  - `[BeforeTestRun]`: PostgreSQL (com `max_connections` alto), RabbitMQ (com porta fixa no host, para parar e subir) e Redis sobem uma vez, com Testcontainers;
  - `[BeforeScenario]`: cria a infraestrutura do cenário (bancos, virtual host, Redis limpo) e sobe Posicao.Worker, Lancamentos.Api e Posicao.Api;
  - `[AfterScenario]`: descarta serviços, bancos e virtual host, sobe o broker se o cenário o parou e, por último, falha o cenário com passo sem binding (D1);
  - `[AfterTestRun]`: descarta os containers.
- **Isolamento por cenário:**
  - bancos novos de Lançamentos e de Posição;
  - virtual host do RabbitMQ e prefixo de filas próprios;
  - Redis limpo (`FLUSHALL`), já que os cenários rodam em série;
  - titulares únicos.
- **Serviços:** Lancamentos.Api, Posicao.Api e Posicao.Worker sobem antes de cada cenário, com `WebApplicationFactory`, e são descartados no fim dele. O Worker está pronto antes do primeiro passo, como no ambiente real. As chamadas vão direto às Apis, com tokens válidos do `EmissorDeTokens`.
- **Falhas que aparecem nos cenários de negócio:**
  - broker fora: o container é parado, e um `[AfterScenario]` sempre o sobe de volta;
  - banco indisponível por um instante: a connection string aponta para uma porta fechada e depois é corrigida, ou o banco é derrubado e restaurado;
  - Redis fora: a Posicao.Api é recriada com a connection string numa porta fechada;
  - Worker parado: a fábrica é descartada e recriada.
- **Relógio:** `FakeTimeProvider` substitui o `TimeProvider` nos cenários com datas.
- **Execução em série** (`xunit.runner.json`), porque os cenários de broker fora param um container compartilhado.

### D6. Passos

- **Organização:** uma classe de bindings por spec, todas com o mesmo `ContextoDoCenario` (estado do cenário) por injeção de contexto do SpecFlow.
- **Texto dos bindings:** usa o texto literal do passo. Expressões só servem para valores que se repetem entre cenários de uma mesma spec.
- **Verificações:** usam a API pública dos serviços, o RabbitMQ e leitura do banco. Não chamam handlers diretamente.
- **Esperas:** aguardam uma condição com prazo, nunca um intervalo fixo, por causa da consistência eventual.

### D7. Nome e convenção

O projeto se chama `Scenarios.Integration.Specs.Tests`. A convenção do `openspec/config.yaml` e o teste `ProjetosDeTeste_SeguemONomePorTipo` passam a aceitar `Scenarios.Integration.Specs.Tests`, além de `<Area>.Unit.Tests` e `Architecture.Tests`.

**Pacotes de volta ou novos:**

- `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq` e `Testcontainers.Redis`;
- `Microsoft.AspNetCore.Mvc.Testing`;
- `RabbitMQ.Client`, para inspecionar filas como a `_error`;
- `Microsoft.Extensions.TimeProvider.Testing`.

## Risks / Trade-offs

- **R1. Volume de passos.** São cerca de 170 linhas de passos. Mitigação: tarefas por spec, `ContextoDoCenario` compartilhado e helpers de HTTP e de espera.
- **R2. Tempo da suíte.** A execução em série com serviços reais leva alguns minutos. Mitigação: containers compartilhados por execução; só bancos, virtual host e serviços são recriados por cenário.
- **R3. Cenários que revelam defeitos.** A correção é tratada fora desta change. O cenário continua falhando até lá.
- **R4. Fronteira negócio/técnica.** Uma spec pode misturar os dois. Passar uma spec para negócio é criar o `.feature` dela; passar para técnica é apagá-lo. As duas mudanças aparecem na revisão.
