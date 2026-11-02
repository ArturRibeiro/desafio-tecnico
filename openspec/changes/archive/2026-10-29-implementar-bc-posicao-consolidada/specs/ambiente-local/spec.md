# Spec Delta

## MODIFIED Requirements

### Requirement: Banco preparado automaticamente
No ambiente local, o esquema do banco de cada Bounded Context DEVE ser criado e atualizado sem passos manuais. As migrações rodam quando o serviço responsável sobe e são refeitas em segundo plano até o banco estar disponível, sem impedir a inicialização tolerante.

#### Scenario: Primeira subida com banco vazio
- **GIVEN** os volumes dos bancos de Lançamentos e de Posição vazios
- **WHEN** o ambiente local sobe
- **THEN** os dois esquemas são criados e os serviços ficam prontos sem nenhum comando adicional

#### Scenario: Banco sobe depois do serviço
- **GIVEN** que o PostgreSQL de um contexto ainda não aceita conexões
- **WHEN** o serviço responsável pelas migrações desse contexto inicia
- **THEN** o processo continua em execução e aplica as migrações assim que o banco fica acessível
