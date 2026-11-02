# 1.1 Subdomains

> **DDD Estratégico** · [Índice](../../ARQUITETURA.md) · Próximo: [1.2 Context Map](02-context-map.md)

O domínio de movimentações financeiras se divide em três Subdomains, classificados pela importância para o negócio.

```mermaid
flowchart LR
    subgraph dominio["Domínio"]
        core["<b>Lançamentos</b><br/>[Core Subdomain]<br/><br/>Registrar movimentações com<br/>consistência e auditabilidade"]
        sup["<b>Posição Consolidada</b><br/>[Supporting Subdomain]<br/><br/>Informar o saldo de uma conta<br/>em qualquer instante"]
        gen["<b>Identidade e Acesso</b><br/>[Generic Subdomain]<br/><br/>Autenticar clientes e garantir<br/>acesso só às próprias contas"]
    end

    core -- "a posição é derivada<br/>dos lançamentos" --> sup
    gen -. "protege o acesso" .-> core
    gen -. "protege o acesso" .-> sup

    classDef core fill:#b8860b,stroke:#7a5a07,color:#fff
    classDef sup fill:#2e8b57,stroke:#1d5a38,color:#fff
    classDef gen fill:#999999,stroke:#6b6b6b,color:#fff
    class core core
    class sup sup
    class gen gen
    style dominio fill:none,stroke:#444,stroke-dasharray:6 4
```

| Subdomain | Classificação | Estratégia |
|---|---|---|
| **Lançamentos** | Core | Maior investimento em modelagem e testes: é onde está o risco financeiro. |
| **Posição Consolidada** | Supporting | Desenvolvido internamente, mas mais simples: é derivado e pode ser reconstruído. |
| **Identidade e Acesso** | Generic | Não é desenvolvido: usa-se um provedor de identidade pronto (JWT). |
