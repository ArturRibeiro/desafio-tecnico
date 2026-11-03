# Divergências: desafio × implementação

Checklist das divergências entre o enunciado (`desafio-arquiteto-software.pdf`) e a solução entregue, levantadas em 03/11/2026. Os requisitos obrigatórios do enunciado estão todos atendidos; os itens abaixo são de interpretação ou de coerência interna.

## Desafio × implementação

- [ ] **1. Falta uma seção "o que faria diferente com mais tempo".** O enunciado pede explicitamente; o conteúdo está espalhado (itens "Documentar" da [ARQUITETURA](ARQUITETURA.md), [DOMINIO 9](DOMINIO.md#9-fora-do-escopo-evoluções-futuras) e [ARQUITETURA 4.4](ARQUITETURA.md#44-no-desafio)). Reunir numa seção única, priorizada.
- [x] **2. Conciliação só documentada.** A rotina que compara o `SaldoDisponivel` de Lançamentos com a posição consolidada ([ARQUITETURA 3.1](ARQUITETURA.md#31-consistência-e-concorrência)) é o mecanismo que detecta inconsistência, risco central do enunciado. Implementar ou justificar a priorização.
- [x] **3. Tabela de resiliência do gateway desatualizada.** [ARQUITETURA 3.2](ARQUITETURA.md#32-indisponibilidade-parcial) marca "Timeouts e circuit breaker (`Microsoft.Extensions.Http.Resilience`)" como Documentar, mas o timeout já existe (`ActivityTimeout` de 10 s em `src/Gateway/Gateway/appsettings.json`) e o pacote citado não é usado. Corrigir a linha: timeout implementado, circuit breaker documentado.
- [x] **4. HTTPS do cliente ao gateway apresentado como controle vigente.** [ARQUITETURA 4.2](ARQUITETURA.md#42-proteção-dos-dados) cita TLS 1.2+ até o gateway, mas o ambiente local usa HTTP e a [4.4](ARQUITETURA.md#44-no-desafio) não o lista como evolução. Deixar explícito que, em produção, o TLS termina no ingress ou load balancer.
- [ ] **5. Alta demanda sem medição.** Os cenários de [ARQUITETURA 3.3](ARQUITETURA.md#33-alta-demanda) estão "Implementado no desenho; sem teste de carga", e lentidão em pico é o problema motivador do enunciado. Adicionar um teste de carga simples (k6 ou NBomber) contra o `docker compose` e documentar os números.

## Spec × código

- [ ] **6. SDK não fixado no repositório.** A spec [`qualidade-de-build`](openspec/specs/qualidade-de-build/spec.md) exige a versão principal do SDK declarada no repositório, mas não há `global.json` (só o CI fixa `10.0.x`). Criar `global.json` com `"version": "10.0.100"` e `"rollForward": "latestFeature"`.

## Menores

- [ ] **7. Comportamentos técnicos sem teste automatizado:** prontidão respondendo `503`, rate limiting e propagação de traces (já declarado na [ARQUITETURA](ARQUITETURA.md#3-atributos-de-qualidade)).
- [ ] **8. Enunciado fora do Git:** `desafio-arquiteto-software.pdf` está na raiz sem versionamento. Decidir se fica fora (adicionar ao `.gitignore`) ou entra no repositório.
