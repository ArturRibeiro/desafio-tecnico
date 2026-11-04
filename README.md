# Sistema de Movimentações Financeiras

Registra créditos e débitos de contas e informa o saldo (posição consolidada) em qualquer instante.

Para saber mais: [ARQUITETURA.md](documentacao/ARQUITETURA.md) (decisões, [trade-offs](documentacao/ARQUITETURA.md#8-trade-offs-e-alternativas) e [linha do tempo](documentacao/ARQUITETURA.md#9-linha-do-tempo)), [DOMINIO.md](documentacao/DOMINIO.md) (regras de negócio e glossário) e [DESENVOLVIMENTO.md](documentacao/DESENVOLVIMENTO.md) (detalhes técnicos).

## Pré-requisitos

[Docker](https://docs.docker.com/get-docker/) 24+ com Compose v2. Para rodar os testes, também o [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 10.0.

## 1. Subir

```bash
docker compose up -d --build
```

A primeira subida compila as imagens e pode levar alguns minutos. Se a porta 8080 estiver ocupada, use outra: `GATEWAY_PORTA=8090 docker compose up -d --build` (e troque 8080 por 8090 nos comandos abaixo).

## 2. Conferir

```bash
docker compose ps
```

Tudo está no ar quando os serviços aparecem como `healthy`, o que leva alguns segundos depois da subida. Só o Jaeger não mostra esse estado; os traces ficam em `http://localhost:16686`.

O comando `curl http://localhost:8080/health/ready` informa só a saúde do gateway: ele responde `"estado":"Healthy"` mesmo com outros serviços fora do ar.

## 3. Usar

Toda chamada exige um token de acesso (JWT). No ambiente local, ele vem de um emissor de desenvolvimento.

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

O saldo pode levar alguns instantes para refletir o último lançamento; o campo `atualizadoAte` mostra até onde ele já está atualizado. Logo depois de abrir a conta, a consulta pode responder `404` (`conta-nao-encontrada`): basta repetir em alguns segundos.

Cada operação precisa de um `Idempotency-Key` próprio. Repetir a mesma chave devolve o resultado original, sem duplicar a operação.

Consultas por instante, fechamentos diários, erros e limites estão em [DESENVOLVIMENTO.md](documentacao/DESENVOLVIMENTO.md#detalhes-da-api).

## 4. Testar

```bash
dotnet test -c Release
```

O Docker precisa estar em execução, porque os testes de integração sobem PostgreSQL, RabbitMQ e Redis em containers. Eles levam alguns minutos.

Teste de carga com k6 (`carga/executar.sh`): veja [DESENVOLVIMENTO.md](documentacao/DESENVOLVIMENTO.md#teste-de-carga).

## 5. Parar

```bash
docker compose down -v
```

Para e remove os containers e **apaga os dados**. Sem o `-v`, os dados dos bancos são mantidos.
