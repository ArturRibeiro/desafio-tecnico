# Spec Delta

## ADDED Requirements

### Requirement: Traces visíveis no ambiente local
O ambiente local DEVE subir, com o mesmo comando dos serviços, um visualizador de traces que recebe os traces de todos os serviços. A interface web dele é documentada no README como apoio ao desenvolvimento.

#### Scenario: Seguir uma operação
- **GIVEN** o ambiente local em execução
- **WHEN** um lançamento é registrado pelo gateway
- **THEN** o visualizador mostra o trace com o gateway, a Lancamentos.Api e o Posicao.Worker
