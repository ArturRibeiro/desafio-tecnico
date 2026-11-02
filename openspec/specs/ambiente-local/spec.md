# ambiente-local Specification

## Purpose

Permite que qualquer pessoa que clone o repositório suba a solução completa (serviços e infraestrutura) na própria máquina com um comando e entenda pelo README como rodar, testar e navegar pela documentação.

## Requirements

### Requirement: Subida completa com um comando
O repositório DEVE permitir subir, com um único comando documentado, todos os serviços (API Gateway, Lancamentos.Api, Posicao.Api e Posicao.Worker) e a infraestrutura de que dependem. Os únicos pré-requisitos são o Docker e o Docker Compose.

#### Scenario: Primeira execução em máquina limpa
- **WHEN** quem clonou o repositório executa o comando de subida documentado no README
- **THEN** todos os serviços e a infraestrutura entram em execução
- **AND** `GET /health/ready` de todos os serviços passa a responder `200`

### Requirement: Infraestrutura isolada por Bounded Context
O ambiente local DEVE ter um banco PostgreSQL separado para cada Bounded Context. Cada serviço só recebe as credenciais do banco do próprio contexto.

#### Scenario: Credenciais por contexto
- **WHEN** o ambiente local está em execução
- **THEN** os serviços de Lançamentos não têm configuradas as credenciais do banco de Posição Consolidada, e vice-versa

### Requirement: Somente o gateway exposto
No ambiente local, DEVE ser publicada na máquina hospedeira apenas a porta do API Gateway, como ponto de entrada das requisições. A exceção são as interfaces de administração da infraestrutura (como o painel do RabbitMQ), documentadas no README como apoio ao desenvolvimento.

#### Scenario: Acesso direto a um serviço interno
- **WHEN** o ambiente local está em execução e alguém tenta acessar a Lancamentos.Api diretamente pela máquina hospedeira
- **THEN** não existe porta publicada para esse serviço

### Requirement: Ordem de inicialização tolerante
Um serviço DEVE iniciar mesmo que a sua infraestrutura ainda não esteja pronta. Ele informa `503` em `/health/ready` até as dependências ficarem acessíveis, sem encerrar o processo.

#### Scenario: Broker sobe depois do serviço
- **WHEN** a Lancamentos.Api inicia antes de o RabbitMQ aceitar conexões
- **THEN** o processo continua em execução e `/health/ready` responde `503`
- **AND** `/health/ready` passa a responder `200` assim que o RabbitMQ aceita conexões, sem reiniciar o serviço

### Requirement: Segredos fora do código
Nenhuma credencial DEVE ficar no código-fonte ou nos arquivos de configuração versionados dos serviços. As credenciais do ambiente local são fornecidas por variáveis de ambiente, e os valores de desenvolvimento ficam num arquivo de exemplo identificado como exclusivo para uso local.

#### Scenario: Busca por credenciais nos arquivos de configuração
- **WHEN** os arquivos de configuração versionados dos serviços são inspecionados
- **THEN** não contêm senhas nem connection strings com credenciais

### Requirement: README como ponto de entrada
O repositório DEVE ter na raiz um README com:
- os pré-requisitos e suas versões;
- como subir o ambiente;
- como compilar e rodar os testes;
- como verificar que os serviços estão de pé;
- como parar e limpar o ambiente;
- links para `DOMINIO.md` e `ARQUITETURA.md`.

#### Scenario: Seguir o README do início ao fim
- **WHEN** uma pessoa sem contexto prévio segue apenas as instruções do README
- **THEN** ela consegue subir o ambiente, rodar os testes e confirmar a saúde dos serviços sem consultar outra fonte

#### Scenario: Navegação para a documentação
- **WHEN** a pessoa lê o README
- **THEN** encontra links para a modelagem de domínio e para a arquitetura

### Requirement: Tokens de desenvolvimento
O ambiente local DEVE oferecer um comando documentado que emite um token JWT aceito pelos serviços locais, para um `TitularId` e escopos escolhidos por quem executa. O token expira e é assinado com uma chave que só existe nos arquivos de desenvolvimento.

#### Scenario: Emitir token e usar a API
- **WHEN** quem desenvolve executa o comando de emissão documentado no README, informando um titular
- **THEN** recebe um token que, enviado ao gateway, é aceito pela Lancamentos.Api como daquele titular

#### Scenario: Token com escopo restrito
- **WHEN** o token é emitido apenas com o escopo `posicao:ler`
- **THEN** as operações de escrita em Lançamentos recusam esse token com `403`

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

### Requirement: Traces visíveis no ambiente local
O ambiente local DEVE subir, com o mesmo comando dos serviços, um visualizador de traces que recebe os traces de todos os serviços. A interface web dele é documentada no README como apoio ao desenvolvimento.

#### Scenario: Seguir uma operação
- **GIVEN** o ambiente local em execução
- **WHEN** um lançamento é registrado pelo gateway
- **THEN** o visualizador mostra o trace com o gateway, a Lancamentos.Api e o Posicao.Worker
