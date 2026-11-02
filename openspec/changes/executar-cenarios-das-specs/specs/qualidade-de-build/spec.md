# Spec Delta

## ADDED Requirements

### Requirement: Cenários de negócio mantidos pelas changes
Toda change que cria ou altera cenários de uma spec de negócio DEVE criar ou atualizar, na mesma change, o `.feature` dessa spec no `Scenarios.Integration.Specs.Tests`, com o texto dos cenários da spec. Specs técnicas não têm `.feature`.

#### Scenario: Spec de negócio nova
- **GIVEN** uma change que cria uma spec de negócio
- **WHEN** a change é implementada
- **THEN** ela inclui o `.feature` com os cenários da spec e os passos que os executam

#### Scenario: Cenário alterado
- **GIVEN** uma change que altera um cenário de uma spec de negócio
- **WHEN** a change é implementada
- **THEN** o `.feature` da spec passa a ter o texto novo do cenário

### Requirement: Cenários de negócio executados
Todo cenário de uma spec de negócio DEVE ser executado pela suíte de testes contra os serviços em funcionamento. Um cenário só passa quando todos os seus passos estão implementados e satisfeitos.

#### Scenario: Cenário de negócio sem implementação
- **GIVEN** uma spec de negócio com um cenário novo
- **WHEN** a suíte de testes é executada sem a implementação dos passos desse cenário
- **THEN** a suíte falha e identifica o cenário

#### Scenario: Spec técnica
- **GIVEN** uma spec técnica, sem `.feature`
- **WHEN** a suíte de testes é executada
- **THEN** os cenários dela não são executados como fluxo de negócio
