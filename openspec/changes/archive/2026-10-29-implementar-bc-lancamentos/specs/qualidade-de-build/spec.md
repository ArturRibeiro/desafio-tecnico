# Spec Delta

## MODIFIED Requirements

### Requirement: Dependências entre camadas apontam para dentro
Em cada Bounded Context, a Aplicação SHALL depender apenas do Domínio. A Infraestrutura SHALL depender apenas da Aplicação, do Domínio e do projeto de contratos de integração, onde traduz Domain Events em Integration Events. Nenhuma camada interna pode depender de uma camada mais externa.

#### Scenario: Aplicação referencia Infraestrutura
- **WHEN** um tipo da Aplicação passa a depender de um tipo da Infraestrutura
- **THEN** os testes de arquitetura falham

#### Scenario: Aplicação referencia os contratos de integração
- **WHEN** um tipo da Aplicação ou do Domínio passa a depender do projeto de contratos de integração
- **THEN** os testes de arquitetura falham, porque o modelo interno não pode conhecer a Published Language

#### Scenario: Infraestrutura traduz para os contratos
- **WHEN** a Infraestrutura depende do projeto de contratos de integração
- **THEN** os testes de arquitetura passam
