# 1.2 Context Map

> **DDD Estratégico** · [Índice](../../ARQUITETURA.md) · Anterior: [1.1 Subdomains](01-subdomains.md) · Próximo: [1.3 C4 — System Context](03-c4-system-context.md)

Cada Subdomain desenvolvido aqui vira um Bounded Context. O mapa mostra quem depende de quem (**U** = Upstream, quem fornece; **D** = Downstream, quem depende) e como eles se integram.

```mermaid
flowchart LR
    lanc["<b>BC Lançamentos</b><br/>[Core]"]
    pos["<b>BC Posição Consolidada</b><br/>[Supporting]"]
    idp["<b>Provedor de Identidade</b><br/>[Generic · externo]"]

    lanc -- "<b>U</b> → <b>D</b><br/>Published Language<br/>ContaAberta v1 e<br/>MovimentacaoRegistrada v1<br/>(RabbitMQ)" --> pos
    idp -- "<b>U</b> → <b>D</b><br/>Open Host Service + Conformist<br/>(tokens JWT / OIDC)" --> lanc
    idp -- "<b>U</b> → <b>D</b><br/>Open Host Service + Conformist<br/>(tokens JWT / OIDC)" --> pos

    classDef core fill:#b8860b,stroke:#7a5a07,color:#fff
    classDef sup fill:#2e8b57,stroke:#1d5a38,color:#fff
    classDef gen fill:#999999,stroke:#6b6b6b,color:#fff
    class lanc core
    class pos sup
    class idp gen
```

| Relação | Padrão | Significado |
|---|---|---|
| Lançamentos → Posição Consolidada | **Published Language** | Lançamentos publica um contrato de eventos com versão. Posição Consolidada depende desse contrato, não do modelo interno de Lançamentos. Não há **Shared Kernel** (código de domínio compartilhado). |
| Provedor de Identidade → os dois BCs | **Open Host Service** + **Conformist** | O provedor usa um protocolo padrão (OIDC/JWT), e os BCs o seguem como é. |
