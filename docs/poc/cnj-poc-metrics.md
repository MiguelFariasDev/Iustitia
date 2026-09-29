# Prova de Conceito CNJ — Métricas

Medições da Etapa 1.3, coletadas em **28/09/2026** contra a API pública real do
DJEN (`comunicaapi.pje.jus.br`). Scripts em `scripts/poc/`.

> **Como ler estes números.** São uma amostra: um ponto de rede, um intervalo de
> algumas horas, contra um serviço público sem SLA. O rate limit foi reprodutível
> em três execuções independentes e pode ser tratado como característica do
> serviço. A latência varia com horário e carga e **não deve ser lida como SLA**.

## 1. Latência da API do CNJ

`scripts/poc/djen-latency.sh 40 1` — 40 requisições sequenciais, 4 tribunais,
página de 100 itens.

| Métrica | Valor |
|---|---|
| Amostras | 40 |
| Taxa de sucesso | **100%** (40 × HTTP 200) |
| Mínimo | 0,333 s |
| **P50** | **0,465 s** |
| P90 | 0,888 s |
| **P95** | **1,827 s** |
| P99 / máximo | 6,406 s |
| Média | 0,752 s |
| Desvio-padrão | 1,029 s |
| Payload (100 itens) | ~402 KB |

Por tribunal:

| Tribunal | n | P50 | Máximo |
|---|---|---|---|
| TJSP | 10 | 0,443 s | 1,508 s |
| TJMG | 10 | 0,420 s | 1,827 s |
| TJRJ | 10 | 0,465 s | **6,406 s** |
| TJCE | 10 | 0,518 s | 3,040 s |

**Leitura:** mediana baixa e cauda longa — o desvio-padrão (1,03 s) é maior que a
mediana (0,47 s). Justifica o timeout folgado de 30 s por tentativa (ADR-059):
dimensionar pelo P50 cortaria requisições que teriam sucesso.

## 2. Rate limit

`scripts/poc/djen-ratelimit.sh` — requisições sequenciais sem pausa até o primeiro `429`.

| Execução | 1ª com `429` | Bem-sucedidas antes | Tempo decorrido | Recuperação |
|---|---|---|---|---|
| 1 | #21 | 20 | 4,74 s | ≤ 5 s |
| 2 | #21 | 20 | 5,29 s | ≤ 5 s |
| 3 | #21 | 20 | 4,51 s | ≤ 5 s |

**Reprodutível: 20 requisições por janela de ~5 s (~4 req/s).**

Rajada concorrente (`djen-latency.sh 60 10`):

| Métrica | Valor |
|---|---|
| Requisições | 60, concorrência 10 |
| HTTP 200 | 20 (33%) |
| **HTTP 429** | **40 (67%)** |
| 5xx / erro de conexão | 0 |

Cabeçalhos do `429`: **não há `Retry-After`**, nem `X-RateLimit-*`. O backoff é
calibrado pela recuperação observada, não por instrução do servidor.

## 3. Defeito do `count` (paginação)

Mesma consulta (TJCE, um dia), variando só `itensPorPagina`:

| `itensPorPagina` | `count` devolvido | Itens recebidos | Total real |
|---|---|---|---|
| 10 | 14.138 | 10 | 14.138 |
| 50 | 14.138 | 50 | 14.138 |
| **100** | **100** | 100 | **14.138** |

Confirmado que as páginas 2 e 3 também devolvem `count=100` com 100 itens cada —
ou seja, o valor é consistente e consistentemente errado.

**Impacto antes da correção:** a captura parava na página 1 e persistia 100 de
14.138 publicações (**0,7% de recall**), sem erro, log ou métrica.

## 4. Captura por processo (o cenário do escritório)

7 processos reais, 4 tribunais, sem janela de datas (histórico completo).

| Processo | Tribunal | Esperadas | Capturadas | Mais antiga |
|---|---|---|---|---|
| 5000230-98.2024.8.13.0382 | TJMG | 16 | 16 | 2025-04-24 |
| 0000777-07.2018.8.06.0100 | TJCE | 6 | 6 | 2026-08-27 |
| 0234493-37.2024.8.06.0001 | TJCE | 5 | 5 | 2026-03-06 |
| 0815916-58.2022.8.19.0004 | TJRJ | 5 | 5 | 2025-06-11 |
| 1697232-12.2015.8.13.0024 | TJMG | 5 | 5 | **2023-03-17** |
| 4005347-92.2026.8.26.0176 | TJSP | 4 | 4 | 2026-08-17 |
| 1506425-76.2025.8.26.0071 | TJSP | 2 | 2 | 2026-09-28 |
| **Total** | | **43** | **43** | |

**Recall: 43/43 = 100%.** Zero inválidas, zero erros.

A publicação mais antiga é de **março de 2023** — 3,5 anos fora de qualquer
janela de `LookbackDays`, o que valida a decisão de a captura por processo
ignorar a janela por padrão.

| Execução | Duração | Novas | Duplicadas | Inválidas | Erros |
|---|---|---|---|---|---|
| 1ª | 15,0 s | **43** | 0 | 0 | 0 |
| 2ª (1 min depois) | 0,88 s | **0** | **43** | 0 | 0 |

**Idempotência: perfeita.** A 2ª execução é 17× mais rápida — o pré-filtro de
página resolve a deduplicação em uma consulta, sem round-trip por publicação.

## 5. Vazão da varredura por tribunal

Varredura do TJCE (um dia, 14.138 publicações disponíveis), `PageSize=100`:

| Métrica | Valor |
|---|---|
| Páginas buscadas | 18 |
| Publicações persistidas | 1.800 |
| Processos distintos alcançados | 1.496 |
| Janela | 191 s |
| **Vazão de requisições** | **0,09 req/s** |
| **Vazão de publicações** | **9,4 pub/s** |
| Intervalo médio por página | 11,2 s |
| Eventos de rate limit | **0** |

Execução interrompida por tempo (limite do script), não por erro — a varredura
seguia normalmente.

**Leitura — o gargalo é nosso, não do CNJ.** A 0,11 req/s, a captura opera a
~3% do teto do CNJ (4 req/s): o limitador local nunca chega a segurar nada na
varredura. O tempo é gasto na persistência — cada página de 100 publicações
dispara 100 comandos MediatR, cada um com sua transação e escrita de outbox.

Extrapolação: 14.138 publicações de um tribunal-dia ≈ **21 min**; quatro
tribunais de porte semelhante ≈ **85 min**. Aceitável para um job noturno às
03:00. Se virar restrição, o caminho é persistir em lote por página, não pedir
mais vazão ao CNJ.

## 6. Qualidade dos dados

43 publicações reais, após normalização do `RawContent`:

| Verificação | Resultado |
|---|---|
| Tags HTML residuais | **0** |
| Entidades HTML não decodificadas | **0** |
| Espaços duplicados | **0** |
| Espaço nas bordas | **0** |
| Acentuação preservada | 42 de 43 (a 43ª não tem acento no original) |
| Encoding corrompido | **0** |
| Texto truncado (< 20 caracteres) | **0** |
| Sem `metadata_json` | **0** |
| Sem `hash` de origem | **0** |
| Sem órgão | **0** |
| Origem diferente de DJEN | **0** |
| Status diferente de `PendingReview` | **0** |
| `published_at` no futuro | **0** |
| `captured_at` anterior a `published_at` | **0** |
| `external_id` distintos | 43 de 43 |

Tamanho do `raw_content`: de 38 a 6.150 caracteres.

## 7. Performance dos índices

50.043 publicações (43 reais + 50.000 sintéticas), após `ANALYZE`.

| Consulta | Plano | Tempo |
|---|---|---|
| Deduplicação, **com** `is_deleted = false` | Index Scan `ux_publications_tenant_external_id` | **0,143 ms** |
| Deduplicação, **sem** `is_deleted = false` | Seq Scan | 43,5 ms |
| Timeline do processo | Index Scan `ix_publications_tenant_process_published` | **0,680 ms** |
| Fila de revisão — índice **antigo** `(tenant_id, process_id)` | Index Scan + top-N heapsort de 2.500 linhas | 25,0 ms |
| Fila de revisão — índice **corrigido** `(tenant_id, published_at)` | Index Scan, sem sort | **0,181 ms** |
| Não lidas | Index Only Scan `ix_publications_tenant_unread` | 14,2 ms |
| Full-text, termo raro (5/50.043) | Bitmap Index Scan `ix_publications_search` | **0,145 ms** |
| Full-text, termo raro, **sem** o índice | Seq Scan | 29,3 ms |

**Dois achados:**

1. O índice parcial da fila de revisão estava chaveado por `process_id`,
   inútil para a ordenação por data — **25 ms → 0,18 ms (139×)** com a coluna
   certa. Corrigido (migration `FixPendingReviewIndex`), ADR-057 atualizada.
2. O índice único de deduplicação é **parcial** (`WHERE is_deleted = false`).
   O EF Core sempre inclui esse predicado pelo filtro global, mas **qualquer
   consulta Dapper/SQL raw que o omita cai em seq scan** — 300× mais lenta.

### Custo de armazenamento

50.043 publicações: tabela **98 MB**, índices **39 MB** (**40%** da tabela).

| Índice | Tamanho |
|---|---|
| `ix_publications_raw_content_trgm` | **16 MB** |
| `ix_publications_search` (GIN full-text) | 7,6 MB |
| `ix_publications_cnj_number_trgm` | 6,2 MB |
| `ux_publications_tenant_external_id` | 4,3 MB |
| `ix_publications_cnj_number` | 2,4 MB |
| `pk_publications` | 2,2 MB |
| `ix_publications_tenant_process_published` | 704 KB |
| `ix_publications_tenant_status` | 344 KB |
| `ix_publications_tenant_unread` (parcial) | 56 KB |
| `ix_publications_tenant_pending_review` (parcial) | **40 KB** |
| `ix_publications_published_brin` | 24 KB |
| `ix_publications_captured_brin` | 24 KB |

**Leitura:** os índices parciais custam ~0,1% do conjunto e servem as duas
consultas mais frequentes — confirma a aposta da ADR-057. Os três GIN somam
30 MB (77% do custo de índice); o trigram sobre `raw_content` sozinho é 16 MB.

**Os dois BRIN nunca foram escolhidos pelo planner** — o B-tree composto cobre
os filtros por faixa de data. Custam 24 KB cada, então ficam; mas hoje são peso
morto, não otimização.

> Ressalva: o texto sintético é uniforme, o que **superestima** o custo dos
> índices trigram/full-text (poucos termos distintos, muitas repetições) e
> distorce a seletividade. Os números de tamanho são ordem de grandeza, não
> previsão.

## 8. Isolamento por tenant (RLS)

Conectando como `app_user` (a role da aplicação), não como dono das tabelas —
o dono bypassa RLS e um teste assim passaria mesmo sem policy.

| Cenário | Esperado | Observado |
|---|---|---|
| Tenant A, `SELECT` sem `WHERE` | só as 43 do A | **43** (de 50.044 na tabela) |
| Tenant B, `SELECT` sem `WHERE` | só a 1 do B | **1**, 1 tenant distinto |
| **Sem tenant na sessão** | 0 linhas (fail closed) | **0**, sem erro |
| `INSERT` com `tenant_id` de outro tenant | negado | **`ERROR: new row violates row-level security policy`** |

O caso "sem tenant" só devolve 0 sem erro por causa do `NULLIF(..., '')` na
policy (ver `011_publications_rls.sql`). Sem ele, uma conexão reciclada do pool
levanta `22P02` — continua *fail closed*, mas quebra a requisição.
