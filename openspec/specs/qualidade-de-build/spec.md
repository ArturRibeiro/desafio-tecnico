# qualidade-de-build Specification

## Purpose

Garante que a solução inteira compila sem erros nem warnings, que os testes rodam com um único comando e que as fronteiras do DDD (camadas e Bounded Contexts) são verificadas automaticamente, e não só documentadas.

## Requirements

### Requirement: Compilação sem warnings
A solução DEVE falhar a compilação diante de qualquer warning do compilador ou dos analisadores, em todos os projetos, inclusive nos de teste.

#### Scenario: Build limpo
- **WHEN** a solução é compilada a partir de um clone limpo do repositório
- **THEN** a compilação termina com sucesso, sem erros e sem warnings

#### Scenario: Warning introduzido
- **WHEN** uma alteração introduz um warning em qualquer projeto
- **THEN** a compilação falha e aponta o warning como erro

### Requirement: Versão do SDK fixada
O repositório DEVE declarar a versão principal do SDK .NET usada na compilação, para que todos os ambientes compilem com a mesma versão principal.

#### Scenario: SDK de outra versão principal instalado ao lado
- **WHEN** a máquina tem mais de uma versão principal do SDK instalada
- **THEN** a compilação usa a versão principal declarada no repositório

### Requirement: Versão principal do MassTransit limitada
A solução DEVE usar o MassTransit apenas na versão principal 8. Uma atualização de pacotes não pode levar a solução para a versão 9.

#### Scenario: Atualização de pacotes
- **WHEN** as dependências são atualizadas para a versão mais recente permitida
- **THEN** o MassTransit permanece na versão principal 8

### Requirement: Domínio isolado
O projeto de Domínio de cada Bounded Context NÃO DEVE depender de nenhum outro projeto da solução nem de frameworks de infraestrutura (web, persistência, mensageria ou cache).

#### Scenario: Domínio referencia infraestrutura
- **WHEN** um tipo do Domínio passa a depender da camada de Aplicação, de Infraestrutura, de Api ou de um framework de infraestrutura
- **THEN** os testes de arquitetura falham e identificam a dependência proibida

### Requirement: Dependências entre camadas apontam para dentro
Em cada Bounded Context, a Aplicação DEVE depender apenas do Domínio e da biblioteca técnica do Mediator. A Infraestrutura DEVE depender apenas da Aplicação, do Domínio e do projeto de contratos de integração, onde traduz Domain Events em Integration Events. Nenhuma camada interna pode depender de uma camada mais externa.

#### Scenario: Aplicação referencia Infraestrutura
- **WHEN** um tipo da Aplicação passa a depender de um tipo da Infraestrutura
- **THEN** os testes de arquitetura falham

#### Scenario: Aplicação referencia os contratos de integração
- **WHEN** um tipo da Aplicação ou do Domínio passa a depender do projeto de contratos de integração
- **THEN** os testes de arquitetura falham, porque o modelo interno não pode conhecer a Published Language

#### Scenario: Infraestrutura traduz para os contratos
- **WHEN** a Infraestrutura depende do projeto de contratos de integração
- **THEN** os testes de arquitetura passam

### Requirement: Bounded Contexts independentes
Nenhum projeto de um Bounded Context DEVE depender de projetos de outro Bounded Context. Há três dependências compartilhadas permitidas: o projeto de contratos de integração (Published Language), a biblioteca técnica do Mediator, usada pelas Aplicações, e a biblioteca técnica de hospedagem, que só pode ser usada por projetos Api e Worker e pelo Gateway.

#### Scenario: Posição referencia Lançamentos
- **WHEN** um projeto de Posição Consolidada passa a depender de um projeto de Lançamentos, ou o contrário
- **THEN** os testes de arquitetura falham

#### Scenario: Uso dos contratos de integração
- **WHEN** um projeto de qualquer Bounded Context depende do projeto de contratos de integração
- **THEN** os testes de arquitetura passam

#### Scenario: Biblioteca de hospedagem usada fora da borda
- **WHEN** um projeto de Domínio, de Aplicação ou de Infraestrutura passa a depender da biblioteca técnica de hospedagem
- **THEN** os testes de arquitetura falham

### Requirement: Mediator compartilhado sem dependências
A biblioteca técnica do Mediator DEVE ser uma só para todos os Bounded Contexts e NÃO DEVE depender de nenhum projeto nem pacote, porque toda Aplicação a referencia.

#### Scenario: Mediator ganha uma dependência
- **GIVEN** a biblioteca do Mediator usada pelas Aplicações
- **WHEN** ela passa a depender de outro projeto ou de um pacote
- **THEN** os testes de arquitetura falham

### Requirement: Biblioteca de hospedagem sem conceitos de domínio
A biblioteca técnica de hospedagem NÃO DEVE depender de nenhum projeto de Bounded Context nem do projeto de contratos de integração. Ela contém apenas código de infraestrutura de hospedagem, como saúde dos serviços e correlação.

#### Scenario: Biblioteca referencia um Bounded Context
- **WHEN** a biblioteca de hospedagem passa a depender de um projeto de qualquer Bounded Context ou dos contratos de integração
- **THEN** os testes de arquitetura falham

### Requirement: Contratos de integração sem dependências
O projeto de contratos de integração NÃO DEVE depender de nenhum outro projeto da solução nem de frameworks de infraestrutura.

#### Scenario: Contrato referencia um Bounded Context
- **WHEN** o projeto de contratos passa a depender de um projeto de qualquer Bounded Context
- **THEN** os testes de arquitetura falham

### Requirement: Testes executáveis com um comando
Todos os testes automatizados DEVEM rodar com um único comando na raiz do repositório. Os únicos pré-requisitos são o SDK .NET e o Docker.

#### Scenario: Execução local dos testes
- **WHEN** quem clonou o repositório executa o comando de testes documentado no README
- **THEN** todos os projetos de teste são executados e o resultado é informado

### Requirement: Integração contínua
Cada push e cada pull request DEVE disparar uma verificação automática que compila a solução com as mesmas regras de warnings e executa todos os testes.

#### Scenario: Pull request com teste quebrado
- **WHEN** um pull request faz um teste falhar ou introduz um warning
- **THEN** a verificação automática do pull request falha

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
