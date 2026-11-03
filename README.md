# Sistema de Movimentações Financeiras

Registra créditos e débitos de contas e informa a posição consolidada (saldo) em qualquer instante. Arquitetura e decisões: [ARQUITETURA.md](ARQUITETURA.md), [DOMINIO.md](DOMINIO.md) e [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md).

## Pré-requisitos

[Docker](https://docs.docker.com/get-docker/) 24+ com Compose v2. Para rodar os testes, também o [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 10.0.

## 1. Subir

```bash
docker compose up -d --build
```

A primeira subida compila as imagens e leva alguns minutos. Com a porta 8080 ocupada, use outra: `GATEWAY_PORTA=8090 docker compose up -d --build` (e troque 8080 por 8090 abaixo).

## 2. Conferir

```bash
curl http://localhost:8080/health/ready
```

Responde `"estado":"Healthy"` quando tudo está no ar. Traces no Jaeger: `http://localhost:16686`.

## 3. Usar

Toda chamada exige um token JWT. No ambiente local, ele vem de um emissor de desenvolvimento.

**Emitir um token:**

```bash
TOKEN=$(docker compose run --rm -q emissor-de-tokens --titular cliente-1)
```

**Abrir uma conta:**

```bash
CONTA=$(curl -s -X POST http://localhost:8080/lancamentos/contas -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: abre-1" | sed -E 's/.*"contaId":"([^"]+)".*/\1/')
```

**Creditar R$ 100,00:**

```bash
curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/creditos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: credito-1" -H "Content-Type: application/json" -d '{"valor": 100.00, "descricao": "Pix recebido"}'
```

**Debitar R$ 30,00:**

```bash
DEBITO=$(curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/debitos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: debito-1" -H "Content-Type: application/json" -d '{"valor": 30.00, "descricao": "Conta de luz"}' | sed -E 's/.*"lancamentoId":"([^"]+)".*/\1/')
```

**Estornar o débito:**

```bash
curl -s -X POST http://localhost:8080/lancamentos/contas/$CONTA/lancamentos/$DEBITO/estornos -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: estorno-1"
```

**Consultar a posição (saldo):**

```bash
curl -s http://localhost:8080/posicao/contas/$CONTA -H "Authorization: Bearer $TOKEN"
```

A posição é atualizada por eventos e pode levar alguns instantes para refletir o último lançamento; o campo `atualizadoAte` mostra até onde ela já está atualizada. Cada operação exige um `Idempotency-Key` próprio: repetir a mesma chave devolve o resultado original.

Consultas por instante, fechamentos diários, erros e limites estão em [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md#detalhes-da-api).

## 4. Testar

```bash
dotnet test -c Release
```

Com o Docker em execução: os testes de integração sobem PostgreSQL, RabbitMQ e Redis com Testcontainers.

## 5. Parar

```bash
docker compose down -v
```

Para e remove os containers e os dados. Sem `-v`, os dados dos bancos são mantidos.
