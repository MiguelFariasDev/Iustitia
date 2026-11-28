# Performance de consultas — medições e decisões

Medido em PostgreSQL 16 com RLS ligado e a role `app_user` (a mesma da aplicação), sobre
300 mil publicações: um escritório grande (150 mil) e um pequeno. 23 consultas equivalentes ao
SQL que o EF Core gera (capturado do log), melhor de 3 execuções, `EXPLAIN (ANALYZE, BUFFERS)`.

## O que foi feito (migrations `OtimizaIndicesConsultas` + SQL 020)

| Mudança | Efeito medido |
|---|---|
| Listas já na ordem da tela: `(tenant_id, published_on DESC, captured_at DESC)` parcial `NOT is_deleted` | elimina o Sort; página 1 em ~0,1 ms |
| Lista por situação com `INCLUDE (is_read)` | contadores do painel em *Index Only Scan*: **198 ms → ~25 ms** no escritório grande |
| Responsável e cliente com a data no índice | `pub_responsavel_pendentes` 0,06 ms (a versão sem data regredia para 4 ms) |
| Alertas: `(tenant, user, created_at DESC)` e parcial de não lidos | ~**220×** mais rápido |
| Lançamentos: `(tenant, tipo, status) INCLUDE (valor)` | totais sem tocar na tabela (11 ms em 150 mil linhas, 0,97 ms no pequeno) |
| Processos: `(tenant, status, area, created_at DESC)` | lista ~**60×** mais rápida |
| Remoção de 5 índices B-tree redundantes e dos 3 GIN + `search_vector` não usados | inserir 30 mil linhas **7,07 s → 1,07 s**; índices **425 → 243 MB** |

Todos os índices novos são parciais em `is_deleted = false` porque o filtro global do EF
(soft delete) sempre o aplica; isso os mantém menores.

## Achado importante: GIN trigram não acelera `LIKE '%x%'` sob RLS

`textlike` (e `texticlike`) **não são leakproof**. Com uma política de RLS ativa, o Postgres só
aplica como condição de índice operadores leakproof antes da política — caso contrário a
função poderia vazar linhas de outro tenant por efeito colateral (erro, tempo). Resultado: para
`app_user` o plano ignora o índice `gin_trgm_ops` (testado com fragmento selectivo: Parallel Seq
Scan / varredura pelo índice do tenant, ~170–330 ms em 150 mil linhas), embora para o superuser o
mesmo índice responda em 1,3 ms. Por isso **não** se criou trigram em `cnj_digits`.
Caminhos se a busca por fragmento virar gargalo: busca por prefixo/igualdade (`text_pattern_ops`),
função `SECURITY DEFINER` que já recebe o tenant, ou marcar o operador como leakproof (decisão de
segurança, exige superuser — não recomendado sem revisão).

## Recomendações no lado do EF Core (NÃO medidas ainda — validar antes de adotar)

1. **Listas**: projetar para um DTO sem `raw_content` (texto de dezenas de KB) em vez de carregar
   a entidade inteira; `AsNoTracking()` em toda consulta de leitura.
2. **Paginação**: `OFFSET` fica caro em páginas profundas (`pub_lista_todas_pag200` = 7,9 ms no
   pequeno); *keyset* `(published_on, captured_at) < (@a, @b)` usa o mesmo índice sem custo extra.
3. **Contagem por página**: `COUNT(*)` a cada página; cachear ou devolver "tem mais" em vez do total.
4. **`SET app.current_tenant` por abertura de conexão** (`TenantContextInterceptor`) é uma ida e
   volta extra e **não é seguro com PgBouncer em modo transaction**. Alternativa: `set_config(..., true)`
   dentro da transação (`SET LOCAL`) ou PgBouncer em modo session.
5. **Filtro de tenant `($1 OR tenant_id=$2) AND tenant_id=$3`**: o `OR $1` pode gerar plano genérico
   ruim com *auto-prepare* do Npgsql; avaliar `Max Auto Prepare` baixo ou consultas compiladas só
   para os caminhos quentes (contadores, sino de alertas).
6. `ExecuteUpdate`/`ExecuteDelete` para operações em massa (ex.: marcar todos os alertas como lidos).

## Reproduzir

Harness e seed ficaram fora do repositório (escopo de medição). A receita: popular com
`generate_series`, rodar como `app_user` com `SET app.current_tenant`, e comparar planos antes/depois.
