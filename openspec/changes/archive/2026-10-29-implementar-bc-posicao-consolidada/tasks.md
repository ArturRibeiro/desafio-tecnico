# Tasks

## 1. Dependências e projetos

- [x] 1.1 Adicionar `Microsoft.Extensions.Caching.StackExchangeRedis` ao `Directory.Packages.props`. Referenciar:
  - EF Core + Npgsql e `MassTransit.EntityFrameworkCore` em `Posicao.Infraestrutura`;
  - o cache e a autenticação JWT na `Posicao.Api`.

  Verificar que `dotnet build -c Release` termina com 0 warnings.
- [x] 1.2 Criar `tests/Posicao.Unidade.Testes`, que referencia `Posicao.Aplicacao`, e adicioná-lo à solução; verificar que `dotnet test` o executa e que `Arquitetura.Testes` continua passando

## 2. Domínio de Posição

- [x] 2.1 Implementar `Resultado`, `Falha`, `CodigosDeFalha` e os Value Objects `ContaId`, `TitularId`, `Valor` e `DataFechamento` (D1); verificar com testes unitários dos limites (valor ≤ 0 recusado, 2 casas decimais, moeda diferente de `BRL` recusada)
- [x] 2.2 Implementar `Titularidade` (`PertenceA`, `UltimaSequenciaAplicada`, `AtualizadoAte`), a Entity `Movimento` e o Aggregate `FechamentoDiario.Aplicar`; verificar com testes unitários de totais e saldo de fechamento
- [x] 2.3 Implementar o serviço `ProjecaoDaConta` (D1). Verificar com testes unitários:
  - aplicação em ordem e parada na lacuna;
  - preenchimento da lacuna aplicando os seguintes;
  - movimento antes da abertura;
  - troca de dia com abertura = fechamento anterior;
  - estorno de débito e de crédito pelo efeito;
  - `competencia-fora-de-ordem`.
- [x] 2.4 Atualizar o `DOMINIO.md`, seção 6, e o diagrama 2.3 com:
  - o estado da projeção na `Titularidade`;
  - o `Efeito` do `Movimento` e o estorno pelo efeito;
  - a definição de `atualizadoAte`;
  - os códigos de falha de Posição;
  - a reconstrução a partir dos movimentos;
  - o reenvio a partir de Lançamentos como evolução (seção 9).

  Verificar que o diagrama renderiza e que os links funcionam.

## 3. Casos de uso

- [x] 3.1 Definir as portas em `Posicao.Aplicacao`: repositórios, unidade de trabalho com lock por conta e cache de saldo de abertura. Implementar `ProjetarAbertura` e `ProjetarMovimentacao` (D2); verificar com testes unitários usando portas falsas, inclusive evento repetido tratado como "já recebido"
- [x] 3.2 Implementar `ObterPosicaoEm` (D5 e D6: abertura pelo cache quando definitiva, gravação no cache só quando definitiva, falha do cache ignorada) e `ObterFechamentosDiarios` (paginação com `tamanho + 1`). Verificar com testes unitários os cenários da spec `consulta-de-posicao`, como:
  - instante dentro do dia;
  - instante antes do primeiro movimento;
  - movimento pendente fora do saldo;
  - cache que lança exceção;
  - páginas.
- [x] 3.3 Implementar `ReconstruirPosicao` (D8) reaproveitando `ProjecaoDaConta`; verificar com testes unitários que a reconstrução de fechamentos alterados devolve o resultado original e que repeti-la não muda nada

## 4. Persistência

- [x] 4.1 Criar `PosicaoDbContext` com `titularidades`, `movimentos` e `fechamentos_diarios`, as chaves e os índices do D4, `numeric(19,2)` e as entidades do Inbox do MassTransit. Gerar a migração inicial em `Migracoes/`, marcada como código gerado. Verificar com `dotnet ef migrations script` que as tabelas e os índices aparecem e que o build segue sem warnings.
- [x] 4.2 Implementar os repositórios, a unidade de trabalho com `pg_advisory_xact_lock` por conta e a tradução da violação de unicidade em "já recebido"; verificar com testes de integração (Testcontainers) que duas transações da mesma conta são serializadas e que um movimento repetido não duplica linhas
- [x] 4.3 Copiar `AplicadorDeMigracoes` e `VerificacaoDeMigracoes` para `Posicao.Infraestrutura` (D4); verificar com um teste de integração que o Worker cria o esquema num banco vazio e que a `Posicao.Api` responde `503` com `migracoes` `Unhealthy` até a migração ser aplicada

## 5. Consumo de eventos no Worker

- [x] 5.1 Implementar a camada anticorrupção, que traduz `ContaAbertaV1` e `MovimentacaoRegistradaV1` para o domínio e lança `EventoInvalidoException`, e os dois consumers; verificar com testes unitários da tradução (tipo desconhecido, valor ≤ 0, moeda e sequência inválidas)
- [x] 5.2 Configurar no `Posicao.Worker`:
  - Inbox EF;
  - retentativa exponencial que ignora `EventoInvalidoException`;
  - `ConcurrentMessageLimit`;
  - migrações na subida;
  - `migracoes` na prontidão.

  Verificar com testes de integração (RabbitMQ e PostgreSQL reais) que um evento repetido é aplicado uma vez e que um evento inválido chega à fila `_error` enquanto outro evento válido é aplicado.
- [x] 5.3 Testar a ordem ponta a ponta no Worker com eventos publicados no broker (fora de ordem; movimento antes da abertura; Worker parado e retomado) e verificar a posição gravada no banco

## 6. Consultas na Posicao.Api

- [x] 6.1 Implementar o grupo `/posicao` com `ExigirEscopo("posicao:ler")`, as duas rotas do D7, a leitura de `instante`, `de`, `ate`, `pagina` e `tamanhoPagina`, e o Problem Details com `codigo` e `correlacaoId`; verificar com testes de integração `200`, `400 instante-invalido`, `400 periodo-invalido` e a forma do JSON
- [x] 6.2 Ligar a autenticação JWT e a titularidade; verificar com testes de integração `401` sem token ou com token expirado, `403` com token só de escrita, `404` idêntico para conta de outro titular e inexistente e `404` para conta ainda não projetada
- [x] 6.3 Configurar o Redis via `IDistributedCache` com timeouts de 250 ms; verificar com testes de integração que a resposta é a mesma com e sem o Redis, que a consulta responde `200` com o Redis parado e que um movimento novo aparece na consulta seguinte

## 7. Reconstrução e ambiente local

- [x] 7.1 Implementar o comando `reconstruir` no `Posicao.Worker`, sem iniciar o bus e limpando `posicao:abertura:*` no Redis quando configurado; verificar com um teste de integração que fechamentos alterados no banco voltam ao valor correto
- [x] 7.2 Atualizar o `docker-compose.yml` e `ambiente/desenvolvimento/`:
  - `autenticacao.env` na `posicao-api`;
  - Redis e `Banco__AplicarMigracoesNaSubida=true` no `posicao-worker`.

  Verificar com `docker compose up -d --build` do zero (`down -v`) que todos os containers ficam `healthy`.
- [x] 7.3 Atualizar o README:
  - seção "Consultar a posição", com curl da posição, posição em um instante e fechamentos;
  - a consistência eventual e o `404` da conta recém-aberta;
  - o comando de reconstrução;
  - a tabela de testes;
  - o estado atual e a tabela de changes.

  Verificar executando os comandos do README do início ao fim no ambiente local.

## 8. Verificação integrada

- [x] 8.1 Fluxo completo pelo gateway, no ambiente local: abrir conta, creditar, debitar e estornar em Lançamentos e consultar a posição atual, a posição em um instante anterior ao débito e os fechamentos. Verificar que os saldos batem com os de Lançamentos e que `atualizadoAte` mostra a última sequência.
- [x] 8.2 Rodar `dotnet build -c Release` e `dotnet test -c Release` na raiz; verificar 0 warnings e todos os testes passando

## Workflow follow-up

- Sincronizar as specs e arquivar a change depois da revisão.
