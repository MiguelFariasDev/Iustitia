# Publicações

Modelo de domínio, ciclo de vida e persistência da entidade central da
Fase 1: a publicação capturada de um diário oficial.

## Modelo de domínio

`Publication` é um **`AggregateRoot<PublicationId>`** que implementa
`IAuditable`, `ISoftDeletable` e `IHasTenant`.

| Campo | Tipo | Observação |
|---|---|---|
| `Id` | `PublicationId` | wrapper de `Guid` |
| `TenantId` | `Guid` | escritório dono da publicação |
| `ProcessId` | `Guid?` | **nulo na Fase 1** — não há cadastro de processos ainda |
| `CnjNumber` | `CNJNumber` | número do processo, validado |
| `ExternalId` | `string` (100) | id da comunicação na origem — chave de deduplicação |
| `Source` | `PublicationSource` | hoje só `DJEN` |
| `RawContent` | `RawContent` | inteiro teor normalizado — **nunca logar** |
| `PublishedAt` | `PublishedAt` | data de disponibilização no diário, em UTC |
| `CapturedAt` | `DateTimeOffset` | quando o Iustitia capturou — distinto de `PublishedAt` |
| `Type` | `PublicationType?` | nulo quando a origem não informa; classificação por IA é Fase 3 |
| `Status` | `PublicationStatus` | estado da revisão humana |
| `IsRead` | `bool` | controle de leitura do usuário — **não** é revisão |
| `MetadataJson` | `string?` (jsonb) | metadados da origem (órgão, classe, link, hash) |
| `ReviewNotes` | `string?` (2000) | anotação da última mudança de status |

### Value objects

**`CNJNumber`** — número único de processo do padrão CNJ (Resolução CNJ
nº 65/2008): `NNNNNNN-DD.AAAA.J.TR.OOOO`, 20 dígitos.

- Aceita entrada **com ou sem máscara** (o DJEN devolve das duas formas em
  campos diferentes) e normaliza sempre para a forma mascarada, que é a
  canônica para exibição e comparação. `Digits` expõe os 20 dígitos puros.
- **Confere os dígitos verificadores** (mod 97 base 10, ISO 7064). Um número
  com DD errado é dado corrompido, não processo desconhecido, e persistir
  isso contaminaria a vinculação com processos na Fase 2. Rejeita também
  segmento `0`, que não existe na Resolução.
- Igualdade é pelos dígitos, não pela formatação da entrada.

**`RawContent`** — inteiro teor normalizado: sem marcação HTML, entidades
decodificadas, espaços colapsados. O DJEN devolve resquícios de HTML
(`<p>`, `<br>`, `&nbsp;`) que variam por tribunal; normalizar na entrada é o
que torna busca full-text e comparação previsíveis.

> **Sigilo:** este valor pode conter dados pessoais e sigilosos de partes do
> processo. Nunca logar, nunca enviar para telemetria, nunca incluir em
> mensagem de erro (claude.md, seção 14). `ToString()` deliberadamente **não**
> devolve o conteúdo — retorna `RawContent(N caracteres)` — porque `ToString()`
> é o caminho mais fácil para o texto vazar num log estruturado.

**`PublishedAt`** — data/hora de disponibilização, sempre UTC. Rejeita datas
no futuro, com tolerância de 5 minutos para desalinhamento de relógio entre o
CNJ, o banco e a aplicação. A contagem de prazos (Fase 3) parte daqui, então
aceitar uma data futura silenciosamente produziria prazos errados — o pior
tipo de erro neste sistema.

## Ciclo de vida

Toda publicação nasce em **`PendingReview`**. Nada avança sem confirmação
explícita do advogado (Provimento CFOAB nº 271/2025 — ver ADR-012).

```
                   ┌──────────────┐
                   │ PendingReview│  (estado inicial de toda captura)
                   └──────┬───────┘
                          │
        ┌─────────────┬───┴────────┬──────────────┐
        ▼             ▼            ▼              ▼
   ┌─────────┐  ┌──────────┐  ┌─────────┐   ┌──────────┐
   │Approved │  │Corrected │  │Rejected │   │ Archived │
   └────┬────┘  └────┬─────┘  └────┬────┘   └──────────┘
        │            │             │              ▲  (terminal)
        └────────────┴─────────────┴──────────────┘
```

Regras, em `Publication.ChangeStatus`:

- De `PendingReview`, qualquer desfecho de revisão é permitido.
- De `Approved`/`Corrected`/`Rejected`, **só** `Archived`.
- `Archived` é **terminal**.
- Nenhuma publicação volta para `PendingReview` depois de revisada — o
  histórico da decisão do advogado é imutável por exigência de auditoria.
- Toda mudança levanta `PublicationStatusChangedEvent`, com estado anterior,
  novo e a anotação.

### `IsRead` não é revisão

`MarkAsRead()` mexe apenas em `IsRead`. Ler uma publicação **não** aprova
nada: confundir os dois transformaria abrir a tela em aprovação tácita, o
oposto da revisão humana explícita exigida. Há teste de domínio garantindo
que `MarkAsRead` não altera `Status`.

### `AttachToProcess`

Na Fase 1 a publicação é persistida "solta" (`ProcessId = null`), porque não
existe cadastro de processos. `AttachToProcess` é idempotente para o mesmo
processo e rejeita a troca para um processo diferente — religar uma
publicação a outro processo é erro de dado, não operação de negócio.

## Eventos de domínio

| Evento | Quando | Observação |
|---|---|---|
| `PublicationCapturedEvent` | publicação nova persistida | consumidores chegam nas Fases 3+ |
| `PublicationStatusChangedEvent` | status de revisão muda | rastro auditável da revisão humana |

Os dois são gravados na outbox na **mesma transação** da publicação (ver
ADR-008). O payload carrega identificadores e metadados, **nunca o
`RawContent`**: o evento é serializado em JSON numa tabela técnica e
propagado por mensageria, dois lugares por onde o inteiro teor de uma
comunicação processual não deve circular.

## Deduplicação

Chave: **`(TenantId, ExternalId, Source)`**. Duas camadas — checagem em código
no handler (que trata duplicata como sucesso, `Existing = true`) e índice
único no banco, que é a camada definitiva sob concorrência.

O racional completo, incluindo por que `tenant_id` e `source` fazem parte da
chave e por que não usamos hash de conteúdo, está na **ADR-056**.

## Índices

Seis índices B-tree/parciais via Fluent API, mais GIN (full-text e trigram) e
BRIN via SQL raw. Cada um justificado por um padrão de acesso real, com o
custo de escrita e espaço discutido explicitamente — ver **ADR-057**.

Resumo:

| Índice | Serve |
|---|---|
| `ux_publications_tenant_external_id` | deduplicação |
| `ix_publications_tenant_process_published` | timeline do processo |
| `ix_publications_tenant_status` | filtro por status |
| `ix_publications_tenant_pending_review` | fila de revisão (parcial) |
| `ix_publications_tenant_unread` | contador de não lidas (parcial) |
| `ix_publications_cnj_number` | busca exata por processo |
| `ix_publications_search` | full-text em português (GIN + tsvector) |
| `ix_publications_raw_content_trgm` | busca por trecho / fuzzy (GIN + pg_trgm) |
| `ix_publications_cnj_number_trgm` | busca parcial por número |
| `ix_publications_published_brin` | filtro por faixa de data |
| `ix_publications_captured_brin` | filtro por faixa de captura |

## Isolamento por tenant

Três camadas independentes, nenhuma sendo a única linha de defesa:

1. `tenantId` explícito no critério de toda specification e de toda consulta
   do repositório;
2. filtro global do `LegalDbContext` (`IHasTenant`);
3. **RLS no Postgres** (`011_publications_rls.sql`) — a camada definitiva,
   porque é a única que `IgnoreQueryFilters()` não contorna.

A policy usa `NULLIF(current_setting('app.current_tenant', true), '')::uuid`.
O `NULLIF` é essencial: o Npgsql reutiliza conexões do pool e o `DISCARD ALL`
reseta um GUC customizado para **string vazia**, não para ausente — sem ele,
`''::uuid` levanta `22P02` na primeira query de uma conexão reciclada sem
tenant. Continua sendo *fail closed* (nada vaza), mas quebra a requisição.

> O mesmo padrão sem `NULLIF` existe em `002_rls.sql` (Etapa 0.2), para as
> tabelas do Platform. Vale aplicar a mesma correção lá.

Há testes de integração conectando como `app_user` (não como dono das
tabelas, que bypassa RLS) que verificam: isolamento na leitura, isolamento
mesmo com `IgnoreQueryFilters()`, *fail closed* sem tenant na sessão, e
rejeição de `INSERT` com `tenant_id` de outro escritório.

## Casos de uso

| Feature | Tipo | Observação |
|---|---|---|
| `CapturePublication` | Command | interno (job/reprocessamento), não exposto na API; carrega `TenantId` explícito porque o job roda sem usuário autenticado |
| `GetPublicationById` | Query | **único** contrato que devolve o `RawContent` completo — é a tela de leitura |
| `ListPublications` | Query | devolve `ContentPreview` (280 caracteres), nunca o texto integral |
| `MarkPublicationAsRead` | Command | só `IsRead` |

`ListPublications` trunca o conteúdo de propósito: uma listagem de 20
publicações com o texto completo trafegaria centenas de KB de dado sigiloso
sem necessidade.

## Códigos de erro

Faixa **PUBLICATION (1300–1399)** — ver `docs/errors/catalog.md`.

## Ainda não implementado

`publication_suggestions` (Fase 3) tem o nome da tabela reservado em
`PublicationSuggestionConfiguration`, mas **nenhuma entidade, DbSet ou tabela**
— criar o esquema antes do agregado existir produziria um palpite que a
Fase 3 teria que migrar de qualquer forma.
