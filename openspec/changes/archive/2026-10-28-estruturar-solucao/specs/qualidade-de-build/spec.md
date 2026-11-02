# Spec Delta

## Purpose

Garante que a solução inteira compila sem erros nem warnings, que os testes rodam com um único comando e que as fronteiras do DDD (camadas e Bounded Contexts) são verificadas automaticamente, e não só documentadas.

## ADDED Requirements

### Requirement: Compilação sem warnings
A solução SHALL falhar a compilação diante de qualquer warning do compilador ou dos analisadores, em todos os projetos, inclusive nos de teste.

#### Scenario: Build limpo
- **WHEN** a solução é compilada a partir de um clone limpo do repositório
- **THEN** a compilação termina com sucesso, sem erros e sem warnings

#### Scenario: Warning introduzido
- **WHEN** uma alteração introduz um warning em qualquer projeto
- **THEN** a compilação falha e aponta o warning como erro

### Requirement: Versão do SDK fixada
O repositório SHALL declarar a versão principal do SDK .NET usada na compilação, para que todos os ambientes compilem com a mesma versão principal.

#### Scenario: SDK de outra versão principal instalado ao lado
- **WHEN** a máquina tem mais de uma versão principal do SDK instalada
- **THEN** a compilação usa a versão principal declarada no repositório

### Requirement: Versão principal do MassTransit limitada
A solução SHALL usar o MassTransit apenas na versão principal 8. Uma atualização de pacotes não pode levar a solução para a versão 9.

#### Scenario: Atualização de pacotes
- **WHEN** as dependências são atualizadas para a versão mais recente permitida
- **THEN** o MassTransit permanece na versão principal 8

### Requirement: Domínio isolado
O projeto de Domínio de cada Bounded Context SHALL não depender de nenhum outro projeto da solução nem de frameworks de infraestrutura (web, persistência, mensageria ou cache).

#### Scenario: Domínio referencia infraestrutura
- **WHEN** um tipo do Domínio passa a depender da camada de Aplicação, de Infraestrutura, de Api ou de um framework de infraestrutura
- **THEN** os testes de arquitetura falham e identificam a dependência proibida

### Requirement: Dependências entre camadas apontam para dentro
Em cada Bounded Context, a Aplicação SHALL depender apenas do Domínio. A Infraestrutura SHALL depender apenas da Aplicação e do Domínio. Nenhuma camada interna pode depender de uma camada mais externa.

#### Scenario: Aplicação referencia Infraestrutura
- **WHEN** um tipo da Aplicação passa a depender de um tipo da Infraestrutura
- **THEN** os testes de arquitetura falham

### Requirement: Bounded Contexts independentes
Nenhum projeto de um Bounded Context SHALL depender de projetos de outro Bounded Context. Há duas dependências compartilhadas permitidas: o projeto de contratos de integração (Published Language) e a biblioteca técnica de hospedagem, que só pode ser usada por projetos Api e Worker e pelo Gateway.

#### Scenario: Posição referencia Lançamentos
- **WHEN** um projeto de Posição Consolidada passa a depender de um projeto de Lançamentos, ou o contrário
- **THEN** os testes de arquitetura falham

#### Scenario: Uso dos contratos de integração
- **WHEN** um projeto de qualquer Bounded Context depende do projeto de contratos de integração
- **THEN** os testes de arquitetura passam

#### Scenario: Biblioteca de hospedagem usada fora da borda
- **WHEN** um projeto de Domínio, de Aplicação ou de Infraestrutura passa a depender da biblioteca técnica de hospedagem
- **THEN** os testes de arquitetura falham

### Requirement: Biblioteca de hospedagem sem conceitos de domínio
A biblioteca técnica de hospedagem SHALL não depender de nenhum projeto de Bounded Context nem do projeto de contratos de integração. Ela contém apenas código de infraestrutura de hospedagem, como saúde dos serviços e correlação.

#### Scenario: Biblioteca referencia um Bounded Context
- **WHEN** a biblioteca de hospedagem passa a depender de um projeto de qualquer Bounded Context ou dos contratos de integração
- **THEN** os testes de arquitetura falham

### Requirement: Contratos de integração sem dependências
O projeto de contratos de integração SHALL não depender de nenhum outro projeto da solução nem de frameworks de infraestrutura.

#### Scenario: Contrato referencia um Bounded Context
- **WHEN** o projeto de contratos passa a depender de um projeto de qualquer Bounded Context
- **THEN** os testes de arquitetura falham

### Requirement: Testes executáveis com um comando
Todos os testes automatizados SHALL rodar com um único comando na raiz do repositório. Os únicos pré-requisitos são o SDK .NET e o Docker.

#### Scenario: Execução local dos testes
- **WHEN** quem clonou o repositório executa o comando de testes documentado no README
- **THEN** todos os projetos de teste são executados e o resultado é informado

### Requirement: Integração contínua
Cada push e cada pull request SHALL disparar uma verificação automática que compila a solução com as mesmas regras de warnings e executa todos os testes.

#### Scenario: Pull request com teste quebrado
- **WHEN** um pull request faz um teste falhar ou introduz um warning
- **THEN** a verificação automática do pull request falha
