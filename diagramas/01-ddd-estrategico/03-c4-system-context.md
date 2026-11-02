# 1.3 C4 — System Context

> **DDD Estratégico** · [Índice](../../ARQUITETURA.md) · Anterior: [1.2 Context Map](02-context-map.md) · Próximo: [1.4 C4 — Container](04-c4-container.md)

Cores e formas seguem a [legenda dos diagramas C4](../../ARQUITETURA.md#legenda-dos-diagramas-c4).

Quem usa o sistema e com quem ele se relaciona.

```mermaid
flowchart TB
    cliente["<b>Cliente</b><br/>[Pessoa]<br/><br/>Correntista do banco digital"]
    sistema["<b>Sistema de Movimentações Financeiras</b><br/>[Sistema]<br/><br/>Registra lançamentos nas contas e informa<br/>a posição consolidada em qualquer instante"]
    idp["<b>Provedor de Identidade</b><br/>[Sistema externo]<br/><br/>Autentica clientes<br/>e emite tokens JWT"]

    cliente -- "Autentica-se<br/>[HTTPS/OIDC]" --> idp
    cliente -- "Registra créditos, débitos e estornos<br/>[HTTPS/JSON]" --> sistema
    cliente -- "Consulta a posição consolidada<br/>em um instante [HTTPS/JSON]" --> sistema
    sistema -- "Valida tokens<br/>[HTTPS/JWKS]" --> idp

    classDef pessoa fill:#08427b,stroke:#052e56,color:#fff
    classDef sistema fill:#1168bd,stroke:#0b4884,color:#fff
    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    class cliente pessoa
    class sistema sistema
    class idp externo
```

| Elemento | Responsabilidade |
|---|---|
| **Cliente** | Registra movimentações e consulta a posição consolidada. Só tem acesso às contas que lhe pertencem. |
| **Provedor de Identidade** | Generic Subdomain: não é desenvolvido aqui, apenas integrado. |

O enunciado não define quais canais o cliente usa (app, internet banking etc.). Por isso o diagrama mostra o Cliente falando diretamente com o sistema: qualquer canal entra como um meio de acesso, sem mudar o modelo. As premissas sobre usuários e acesso estão em [DOMINIO.md](../../DOMINIO.md#premissas).
