# Spec Delta

## ADDED Requirements

### Requirement: Tokens de desenvolvimento
O ambiente local SHALL oferecer um comando documentado que emite um token JWT aceito pelos serviços locais, para um `TitularId` e escopos escolhidos por quem executa. O token expira e é assinado com uma chave que só existe nos arquivos de desenvolvimento.

#### Scenario: Emitir token e usar a API
- **WHEN** quem desenvolve executa o comando de emissão documentado no README, informando um titular
- **THEN** recebe um token que, enviado ao gateway, é aceito pela Lancamentos.Api como daquele titular

#### Scenario: Token com escopo restrito
- **WHEN** o token é emitido apenas com o escopo `posicao:ler`
- **THEN** as operações de escrita em Lançamentos recusam esse token com `403`

### Requirement: Banco preparado automaticamente
No ambiente local, o esquema do banco de Lançamentos SHALL ser criado e atualizado sem passos manuais. As migrações rodam quando o serviço sobe e são refeitas em segundo plano até o banco estar disponível, sem impedir a inicialização tolerante.

#### Scenario: Primeira subida com banco vazio
- **WHEN** o ambiente local sobe com o volume do banco de Lançamentos vazio
- **THEN** o esquema é criado e a Lancamentos.Api fica pronta sem nenhum comando adicional

#### Scenario: Banco sobe depois do serviço
- **WHEN** a Lancamentos.Api inicia antes de o PostgreSQL aceitar conexões
- **THEN** o processo continua em execução e aplica as migrações assim que o banco fica acessível
