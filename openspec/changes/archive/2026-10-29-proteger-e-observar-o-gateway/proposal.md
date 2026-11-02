## Why

Os dois Bounded Contexts funcionam, mas o sistema não tem proteção contra clientes abusivos, e as falhas passam em silêncio. O ARQUITETURA.md ainda lista como pendentes o rate limiting (seção 3.3) e a observabilidade (seção 3.4). Sem eles, um pico de requisições de um único cliente afeta todos os outros, e não há como seguir uma operação desde o gateway até a Posição Consolidada.

## What Changes

- O gateway passa a limitar as requisições de cada cliente. Quem passa do limite recebe uma recusa que informa quando tentar de novo.
- O cliente é identificado pelo titular do token, quando o token é válido. Sem token válido, é identificado pelo endereço de origem.
- O gateway valida o token só para identificar o cliente. Ele continua sem recusar requisições por falta de autenticação; isso segue nos serviços.
- A saúde dos serviços fica fora do limite.
- Todos os serviços passam a gravar logs estruturados, cada linha com o identificador de correlação e do trace.
- Os logs nunca contêm token, titular, valores nem descrições de lançamentos.
- Uma operação vira um único trace distribuído: gateway, serviço, banco, broker e consumer no Worker, mesmo com a entrega adiada pelo Outbox.
- O ambiente local ganha um visualizador de traces.
- O README e o ARQUITETURA.md passam a refletir o que foi implementado.

## Capabilities

### New Capabilities

- `observabilidade`: logs estruturados sem dados sensíveis, correlação nos logs e rastreamento distribuído ponta a ponta.

### Modified Capabilities

- `api-gateway`: limite de requisições por cliente, identificação do cliente e resposta ao exceder o limite.
- `ambiente-local`: visualização dos traces no ambiente local.

## Impact

- **Código:**
  - o gateway;
  - a biblioteca de hospedagem compartilhada;
  - o registro de logs e traces dos quatro serviços.
- **Dependências:** pacotes OpenTelemetry.
- **Ambiente local:**
  - um container novo para os traces, com a interface web exposta;
  - credenciais de autenticação para o gateway.
- **Testes:**
  - novos cenários no projeto de testes do gateway;
  - um projeto novo que observa o trace e os logs de Lançamentos até a Posição Consolidada.
- **Contratos e regras de negócio:** sem mudança.

## Fora do escopo

- Recusar no gateway requisições sem autenticação ou sem escopo. Isso continua nos serviços.
- Métricas e alertas de saúde da integração (Outbox, filas, atraso da posição). Ficam documentados.
- Timeouts adaptativos e circuit breaker no gateway.
- Limite distribuído entre várias instâncias do gateway. O limite é por instância.
- Teste de carga.
