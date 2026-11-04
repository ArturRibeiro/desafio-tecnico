## ADDED Requirements

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
