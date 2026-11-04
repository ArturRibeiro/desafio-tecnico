# teste-de-carga Specification

## Purpose

Mede como a solução se comporta sob alta demanda e sob indisponibilidade do consolidado, de forma reproduzível no ambiente local, e deixa os resultados registrados como evidência. Os números servem de referência e para encontrar o gargalo, não como capacidade de produção.

## Requirements

### Requirement: Execução reproduzível sob demanda
O teste de carga DEVE rodar contra o ambiente local com um único comando documentado, tendo como pré-requisitos só o Docker e o ambiente no ar. Ele NÃO DEVE fazer parte da subida normal do ambiente nem do CI.

#### Scenario: Rodar o teste de carga
- **GIVEN** o ambiente local no ar
- **WHEN** quem clonou o repositório executa o comando documentado
- **THEN** os quatro cenários rodam em sequência e os resultados são gravados no repositório

#### Scenario: Subida normal do ambiente
- **WHEN** o ambiente local é subido com o comando do README
- **THEN** nenhum componente do teste de carga entra em execução

### Requirement: Cenários de carga
O teste DEVE executar quatro cenários: escrita (créditos e débitos) em muitas contas; débitos concorrentes numa mesma conta; consultas de posição junto com a escrita; e escrita com o consolidado parado, que é religado ao final. Toda operação de escrita DEVE usar uma chave de idempotência própria.

#### Scenario: Débitos concorrentes numa mesma conta
- **GIVEN** uma conta com saldo para parte dos débitos disparados ao mesmo tempo
- **WHEN** o cenário termina
- **THEN** só os débitos que cabem no saldo são aceitos, os demais são recusados por saldo insuficiente, e nenhum termina em erro do servidor

#### Scenario: Consolidado parado durante a carga
- **GIVEN** o consolidado parado
- **WHEN** a escrita roda sob carga
- **THEN** Lançamentos continua aceitando as operações, sem erros do servidor
- **AND** depois que o consolidado é religado, a posição das contas alcança o saldo de Lançamentos, e o tempo até isso acontecer é medido

### Requirement: Carga distribuída entre titulares
A carga DEVE ser distribuída entre vários titulares, um por usuário virtual, para medir o sistema e não o limite de requisições por titular do gateway. Respostas de limite excedido DEVEM ser contadas e aparecer nos resultados.

#### Scenario: Limite por titular
- **WHEN** a carga roda dentro do limite de cada titular
- **THEN** os resultados não mostram respostas de limite excedido

### Requirement: Verificação de consistência
Ao final, o teste DEVE comparar o saldo de Lançamentos com a posição consolidada numa amostra de contas, depois de a posição alcançar o atraso. O resultado da comparação DEVE aparecer nos resultados, com quantas contas foram conferidas e quantas divergiram.

#### Scenario: Saldos conferidos
- **GIVEN** a carga encerrada e o consolidado em execução
- **WHEN** a verificação roda
- **THEN** cada conta da amostra tem a posição igual ao saldo de Lançamentos, ou a divergência é registrada nos resultados

### Requirement: Evidência dos resultados
A própria execução DEVE gerar uma tabela de resultados com, por cenário, os usuários virtuais, a duração, a vazão, as latências p50 e p95, os erros e uma observação, além do tempo até a posição alcançar o atraso e da linha de consistência. A execução DEVE gravar também o resumo bruto de cada cenário, a data e o ambiente da medição (CPU, memória e versão do Docker), versionados como prova dos números.

#### Scenario: Tabela gerada pela execução
- **WHEN** o teste de carga termina
- **THEN** a tabela de resultados e os resumos brutos de cada cenário estão gravados, com a data e o ambiente da medição

#### Scenario: Cenário com erros do servidor
- **GIVEN** um cenário em que alguma requisição termina em erro do servidor
- **WHEN** o teste termina
- **THEN** a tabela mostra a quantidade de erros desse cenário e a execução indica falha

### Requirement: Resultados na documentação
A documentação de arquitetura DEVE apresentar a tabela de resultados nos cenários de alta demanda, com a data, o ambiente da medição e o comando para reproduzir, e DEVE dizer explicitamente que os números vêm de um notebook com o ambiente local e servem de referência e para encontrar o gargalo, não como capacidade de produção.

#### Scenario: Leitura da arquitetura
- **WHEN** alguém lê os cenários de alta demanda na documentação de arquitetura
- **THEN** encontra os números medidos, o ambiente, o comando para reproduzir e o aviso sobre o alcance dos números

### Requirement: Correção de gargalo medida antes e depois
Quando uma correção atacar um gargalo encontrado pelo teste de carga, o resultado anterior DEVE ser preservado como "antes" e a execução com a correção DEVE ser registrada como "depois", com os mesmos cenários e parâmetros. A documentação de arquitetura DEVE apresentar a comparação das duas.

#### Scenario: Comparação publicada
- **GIVEN** um gargalo registrado nos resultados do teste de carga
- **WHEN** a correção é entregue
- **THEN** os resultados antes e depois estão gravados lado a lado e a arquitetura mostra a diferença no cenário afetado

#### Scenario: Correção sem efeito
- **GIVEN** uma correção que não melhora o cenário afetado na medição
- **WHEN** a nova execução termina
- **THEN** a correção não é entregue e o gargalo continua registrado como aberto
