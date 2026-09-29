# ADR-057: Estratégia de Índices para Publications

**Status:** Aceito — implementado na Etapa 1.2, **corrigido na Etapa 1.3** (ver "Correção" no fim)

Especialização da ADR-026 (estratégia geral de índices) para a tabela
`publications`, que tem um perfil diferente de todas as tabelas da Fase 0.

## Contexto

`publications` é a primeira tabela do sistema que cresce **sem teto natural**:

- Um processo judicial acumula de dezenas a centenas de publicações ao longo
  da vida.
- Um escritório de 15 advogados acompanha centenas a milhares de processos.
- A captura é diária e nunca para.
- Cada linha carrega o **inteiro teor** da comunicação em `raw_content`
  (`text`), o que faz a tabela crescer em bytes muito mais rápido que em
  número de linhas.

O perfil de acesso é bem definido e assimétrico:

- **Escrita:** rajadas grandes (milhares de inserções por execução do job),
  com updates esparsos depois (marcar como lida, mudar status de revisão). Na
  prática, quase append-only.
- **Leitura:** timeline de um processo, fila de revisão pendente, contador de
  não lidas, busca textual pelo conteúdo, filtro por faixa de data.

Índice é troca: cada um acelera leitura e encarece escrita, espaço e vacuum.
Com rajadas de milhares de inserções, indexar sem critério tornaria a captura
o gargalo.

## Decisão

Cinco tipos de índice, cada um justificado por um padrão de acesso real, em
dois pontos de verdade (mesma divisão da ADR-026):

### Via Fluent API do EF Core (`PublicationConfiguration`)

| Índice | Definição | Serve |
|---|---|---|
| `ux_publications_tenant_external_id` | UNIQUE `(tenant_id, external_id, source)` WHERE `is_deleted = false` | Deduplicação da captura (ver ADR-056) |
| `ix_publications_tenant_process_published` | `(tenant_id, process_id, published_at DESC)` | Timeline de um processo, já ordenada |
| `ix_publications_tenant_status` | `(tenant_id, status)` | Filtro por status na listagem |
| `ix_publications_tenant_pending_review` | parcial `(tenant_id, published_at)` WHERE `status = 'PendingReview'` | Fila de revisão humana — **coluna corrigida na Etapa 1.3** |
| `ix_publications_tenant_unread` | parcial `(tenant_id, is_read)` WHERE `is_read = false` | Contador/badge de não lidas |
| `ix_publications_cnj_number` | `(cnj_number)` | Busca exata por número de processo |

Os dois **índices parciais** são a peça mais importante em termos de custo:
publicação pendente de revisão e publicação não lida são, em regime
estacionário, uma fração pequena da tabela. O índice parcial indexa só essa
fração — fica pequeno o bastante para caber em memória e não cresce junto com
o histórico arquivado.

### Via SQL raw (`infra/supabase/migrations/010_publications_indexes.sql`)

- **GIN + tsvector** (`ix_publications_search`) para busca full-text em
  português, sobre uma **coluna gerada `STORED`**
  (`to_tsvector('portuguese', raw_content)`). Coluna gerada, e não índice de
  expressão: calcular o tsvector de um texto de dezenas de KB é caro, e
  `STORED` paga esse custo uma única vez na escrita — a publicação é imutável
  depois de capturada.
- **GIN + pg_trgm** (`ix_publications_raw_content_trgm`,
  `ix_publications_cnj_number_trgm`) para busca por trecho parcial e
  tolerante a erro de digitação. Complementa o full-text: `to_tsvector` casa
  palavras inteiras após stemming, trigram casa pedaços — necessário para
  achar um fragmento de nome de parte ou um número citado no meio do texto.
- **BRIN** (`ix_publications_published_brin`,
  `ix_publications_captured_brin`) porque as duas colunas crescem de forma
  quase monotônica e a tabela é append-only na prática. BRIN ocupa alguns KB
  onde um B-tree ocuparia centenas de MB, ao custo de scans menos seletivos
  — troca certa para filtros por **faixa** de data (relatórios, expurgo).
  Não substitui o B-tree composto, que é quem serve a **ordenação** dentro de
  um processo.
- **Autovacuum mais agressivo** (`autovacuum_vacuum_scale_factor = 0.05`)
  pelo mesmo motivo de `audit_logs` (ver `003_autovacuum.sql`): sem isso, os
  índices GIN acumulam bloat entre vacuums.

## Consequências

**Positivas:**

- Cada consulta do módulo tem índice: as specifications de domínio foram
  escritas para casar com eles (`PublicationsByProcessSpecification` ordena
  por `published_at DESC`, exatamente a forma do índice composto).
- Os índices parciais mantêm o custo de escrita e o tamanho sob controle
  mesmo com a tabela crescendo indefinidamente.
- A busca por conteúdo — funcionalidade central (Busca Global / Ctrl+K) —
  não degrada para seq scan lendo todo o `raw_content` da tabela.

**Negativas / trade-offs:**

- **Custo de espaço alto.** `search_vector` (~30% do texto) + GIN trigram
  sobre uma coluna `text` grande são, juntos, o item mais caro da tabela em
  espaço e em tempo de escrita. Aceito porque a alternativa (busca sem
  índice) é inviável, mas é o primeiro lugar a revisar se o custo de
  armazenamento do Supabase virar problema.
- **Escrita mais lenta na captura.** Cada inserção atualiza 6 índices B-tree
  mais 3 GIN mais o tsvector gerado. Com páginas de 100 itens e execução
  noturna isso é irrelevante hoje; se a captura crescer para dezenas de
  milhares por execução, vale considerar `INSERT` em lote com os índices GIN
  temporariamente desabilitados.
- **Dois pontos de verdade** (migration do EF Core + SQL do Supabase), com a
  ordem importando: a tabela precisa existir antes do SQL rodar. Os fixtures
  de teste de integração aplicam nessa ordem explicitamente.
- `search_vector` não é conhecida pelo modelo do EF Core; consultas
  full-text precisarão de SQL raw ou `EF.Functions` (Fase 2+).

## Como verificar

Os índices criados pelo EF Core são exercitados pelos testes de integração
do repositório. O padrão de `IndexTests` (Etapa 0.2), que verifica existência
e método de acesso diretamente no `pg_indexes`, deve ser estendido para
`publications` quando a Fase 2 adicionar as consultas que dependem dos
índices GIN/BRIN — hoje eles são criados e documentados, mas nenhuma
consulta da aplicação os usa ainda.


---

## Correção (Etapa 1.3 — medido, não suposto)

A prova de conceito mediu esta estratégia com 50 mil publicações
(`docs/poc/cnj-poc-metrics.md`, seção 7). Três resultados mudaram o que estava
escrito acima.

### 1. O índice da fila de revisão estava errado — corrigido

Estava chaveado por `(tenant_id, process_id)`. A consulta da fila
(`PublicationsPendingReviewSpecification`) filtra por tenant e ordena por
`published_at`; `process_id` não ajuda em nada nessa ordenação, então o Postgres
lia **todas** as pendentes e ordenava em memória.

| Índice | Plano | Tempo |
|---|---|---|
| `(tenant_id, process_id)` | Index Scan + top-N heapsort de 2.500 linhas | 25,0 ms |
| `(tenant_id, published_at)` | Index Scan, já ordenado, lê só as 20 do LIMIT | **0,18 ms** |

**139× mais rápido.** Corrigido pela migration `FixPendingReviewIndex`.

A lição é geral: um índice parcial precisa ser chaveado pela coluna de
**ordenação** da consulta que ele serve, não pela coluna que parecia relacionada
ao domínio.

### 2. O índice único é parcial — e isso tem uma armadilha

`ux_publications_tenant_external_id` tem filtro `WHERE is_deleted = false`. O EF
Core sempre inclui esse predicado pelo filtro global, então a aplicação usa o
índice normalmente (**0,14 ms**). Mas **qualquer consulta Dapper ou SQL raw que
omita `is_deleted = false` cai em seq scan** — 43,5 ms, 300× mais lenta.

Vale para toda consulta de deduplicação escrita fora do EF Core.

### 3. Os dois índices BRIN nunca foram escolhidos pelo planner

`ix_publications_published_brin` e `ix_publications_captured_brin` custam 24 KB
cada e não foram usados em nenhuma consulta medida: o B-tree composto
`(tenant_id, process_id, published_at DESC)` cobre os filtros por faixa de data
com Index Only Scan. **Mantidos** pelo custo desprezível, mas são peso morto até
que exista uma consulta por data sem recorte de tenant (expurgo, relatório
global) — a hipótese que justificou criá-los segue não confirmada.

### Custo real de armazenamento

50.043 publicações: tabela 98 MB, índices 39 MB (**40%**). Os três GIN somam
30 MB (77% do custo de índice), sendo o trigram sobre `raw_content` 16 MB
sozinho. Os dois índices **parciais** custam 40 KB e 56 KB — 0,1% do conjunto —
e servem as duas consultas mais frequentes do produto: a aposta central desta
ADR se confirmou.

> O texto sintético é uniforme, o que superestima o custo dos índices de texto.
> Os tamanhos são ordem de grandeza, não previsão.
