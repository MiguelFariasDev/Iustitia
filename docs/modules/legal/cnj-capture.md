# Captura de Publicações do CNJ

Fluxo do job recorrente que consulta o DJEN (Diário de Justiça Eletrônico
Nacional) e persiste as publicações novas.

## Componentes

```
CnjCaptureJob (Legal.Infrastructure/Jobs)
   │  depende de ICnjClient — a PORTA, em Integrations.CNJ.Application
   ▼
DjenClient (Integrations.CNJ.Infrastructure/Djen)
   │  HttpClient + pipeline Polly 8 (timeout, retry, circuit breaker)
   ▼
API pública de comunicações do CNJ
   https://comunicaapi.pje.jus.br/api/v1/comunicacao
```

O job nunca conhece a implementação HTTP. Quem registra o `DjenClient` é o
host (`AddCnjIntegration`), o que mantém `Legal.Infrastructure` livre da
infraestrutura de outro módulo — regra verificada por teste de arquitetura.

## Fluxo

1. Lê `Jobs:CnjCapture`. Se desabilitado, ou sem `TenantIds` configurados,
   registra e encerra sem tocar o CNJ.
2. Resolve o intervalo de datas (ver **Janela de busca**).
3. Para cada **tenant** configurado:
   - `tenantContext.SetTenant(tenantId)` — **obrigatório**: o job roda fora de
     uma requisição HTTP, e sem isso o filtro global do `LegalDbContext` e o
     RLS do Postgres não têm tenant para aplicar. As consultas de deduplicação
     voltariam vazias (*fail closed*) e o job recriaria tudo.
   - Para cada **tribunal** configurado:
     - Para cada **página** (até `MaxPages`):
       - `ICnjClient.GetPublicationsAsync`
       - pré-filtra a página inteira com `FindExistingExternalIdsAsync`
         (uma consulta, não uma por item)
       - para cada item novo, envia `CapturePublicationCommand`
       - para se a página vier vazia ou não houver mais páginas
4. Registra totais (novas, duplicadas, inválidas, erros) e a duração.
5. Se houve falha de integração, **relança** para o Hangfire marcar a execução
   como falha e aplicar retry.

## Janela de busca

Dois modos, com `StartDate`/`EndDate` tendo precedência:

- **Relativo (recomendado, padrão):** `LookbackDays` dias para trás
  terminando hoje. `LookbackDays = 3` é deliberadamente **sobreposto** entre
  execuções, porque tribunais publicam com atraso — buscar apenas "ontem"
  perderia o que entrou no diário com dois dias de defasagem. A sobreposição
  é barata porque a deduplicação absorve a repetição (ADR-056).
- **Fixo:** `StartDate` + `EndDate`, para recarga histórica pontual. Datas
  fixas em `appsettings` silenciosamente param de capturar quando o período
  passa, então não servem para execução recorrente.

## Paginação

`pagina` é 1-based; `itensPorPagina` é limitado a `MaxPageSize` (100, o
máximo da API) pelo próprio cliente.

A decisão de continuar vem de `CnjPublicationPage.HasMorePages`: usa o `count`
total quando a API o informa; quando vem zerado — o CNJ nem sempre devolve
`count` confiável em consultas amplas — assume que há mais páginas se a página
atual veio cheia.

`MaxPages` (50) é um teto por tribunal e por execução, para o job não rodar
indefinidamente com um filtro amplo demais. Ao atingir o teto, registra um
**aviso** dizendo que há publicações não capturadas no período — sinal de que
`LookbackDays` ou a lista de tribunais precisa ser ajustada.

## Tratamento de erro

Três níveis, propositalmente diferentes:

| Nível | Exemplo | Comportamento |
|---|---|---|
| **Publicação inválida** | CNJ com dígito verificador errado, data no futuro, conteúdo vazio | Conta em `invalid`, registra aviso com o `ExternalId`, **segue** para a próxima. Dado ruim de uma comunicação não custa as outras centenas da página. |
| **Item malformado na resposta** | sem `id`, sem número de processo, sem texto | Descartado pelo `DjenClient` com aviso agregado, sem falhar a página |
| **Falha de integração** | CNJ fora do ar, timeout, 429, resposta inválida | Relança `IntegrationException`. O Hangfire retenta; o que já foi persistido permanece, e na retentativa vira duplicata. |

Uma falha em **um tribunal** não impede os demais: a exceção é guardada e
relançada só no final, depois de tentar todos. Assim uma instabilidade
pontual do TJSP não custa a captura do TJRJ.

Todo o fluxo é **idempotente**, o que é o que torna o retry trivialmente
seguro (ADR-056).

### Resiliência HTTP (`CnjResiliencePipeline`)

Polly 8, via `AddStandardResilienceHandler`. Ordem: timeout total > circuit
breaker > retry > timeout por tentativa.

- **Retry:** 3 tentativas, backoff exponencial **com jitter** — sem jitter, as
  tentativas de tribunais diferentes sincronizariam em rajadas contra uma API
  já instável.
- **Circuit breaker:** abre com 50% de falhas na janela de amostragem, fecha
  após 60s.
- **Transiente** = `408`, `429`, `500`, `502`, `503`, `504`, mais falha de rede
  e timeout. **4xx (exceto 408/429) não é retentado**: é erro de requisição
  nosso, e repetir só gasta quota.
- O `HttpClient.Timeout` é `InfiniteTimeSpan` de propósito — o timeout fica a
  cargo do pipeline, senão o timeout do `HttpClient` cortaria o retry pela
  metade.

### Health check

`CnjHealthCheck` usa `ICnjClient.PingAsync` (consulta mínima de 1 item).
Registrado com a tag **`cnj`**, e **não** `ready`: o CNJ é instável e a
captura é assíncrona e idempotente — o Iustitia continua utilizável com o
DJEN fora do ar. Marcar a aplicação como *not ready* faria o Azure Container
Apps retirar a instância de rotação por uma dependência de terceiro que não
afeta nenhuma requisição de usuário. Falha vira `Degraded`, que alimenta o
alerta sem derrubar nada.

## Observabilidade

### Spans

`ActivitySource`: **`Advocacia.CNJ.Capture`** (registrado em
`OpenTelemetryConfiguration`).

| Span | Atributos |
|---|---|
| `cnj.capture.job` | datas, contagem de tribunais/tenants, totais, status de erro |
| `cnj.capture.page` | tribunal, página, itens, novas, duplicadas |
| `cnj.capture.publication` | `ExternalId` |

### Métricas

Meter `Advocacia`, todas com tag `tribunal`:

| Métrica | Tipo |
|---|---|
| `cnj.capture.publications.count` | counter — novas persistidas |
| `cnj.capture.duplicates.count` | counter — ignoradas por já existirem |
| `cnj.capture.invalid.count` | counter — rejeitadas por dado inválido |
| `cnj.capture.errors.count` | counter — falhas de integração/persistência |
| `cnj.capture.duration` | histogram (ms) — execução completa |

`invalid` merece alerta próprio: uma subida repentina indica mudança no
formato da API do CNJ ou um tribunal publicando número de processo fora do
padrão — não é ruído, é sinal.

### Logs (Serilog)

Início e fim do job, totais por execução, aviso por publicação rejeitada
(com `ExternalId`, tribunal e código do erro) e aviso ao atingir `MaxPages`.

> **Nunca é registrado o `raw_content`** — nem no cliente, nem no handler, nem
> no job, nem em mensagem de validação, nem em atributo de span. O
> `RawContent.ToString()` é sobrescrito justamente para tornar um vazamento
> acidental difícil. Ver claude.md, seção 14.

## Configuração

```json
{
  "Jobs": {
    "CnjCapture": {
      "Enabled": false,
      "CronExpression": "0 3 * * *",
      "LookbackDays": 3,
      "Tribunais": [ "TJSP", "TJRJ", "TJMG", "TJCE" ],
      "PageSize": 100,
      "MaxPages": 50,
      "TenantIds": []
    }
  },
  "Cnj": {
    "BaseUrl": "https://comunicaapi.pje.jus.br/",
    "PublicationsPath": "api/v1/comunicacao",
    "MaxPageSize": 100,
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "RetryBaseDelaySeconds": 2,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerSamplingDurationSeconds": 60,
    "CircuitBreakerMinimumThroughput": 10,
    "CircuitBreakerBreakDurationSeconds": 60
  }
}
```

`Enabled` vem **`false`** por padrão: sem `TenantIds` o job não tem para quem
capturar, e deixá-lo ligado só produziria um aviso diário. Para habilitar,
preencha `TenantIds` com os escritórios e ligue a flag.

`CronExpression` tem 5 campos — Hangfire (via NCrontab) não suporta segundos.

> **Atribuição tenant ↔ publicação:** na Fase 1 (prova de conceito) vem de
> configuração. A partir da Fase 2 passa a derivar do cadastro de processos e
> advogados de cada escritório, e `TenantIds` deixa de existir.

## Registro do job

```csharp
// Hosts/Worker/Program.cs
scope.ServiceProvider.GetRequiredService<LegalRecurringJobsRegistrar>().RegisterAll();
```

O registrar **remove** o job do storage do Hangfire quando `Enabled = false`,
em vez de apenas não registrá-lo — sem isso, desligar a flag deixaria o
agendamento anterior ativo para sempre.

## Como testar

- `Integrations.CNJ.UnitTests` — `DjenClient` contra um
  `HttpMessageHandler` stub: montagem da query string, parsing, descarte de
  item malformado, mapeamento de cada status HTTP para código do catálogo,
  timeout, cancelamento.
- `Legal.IntegrationTests/Jobs/CnjCaptureJobTests` — ponta a ponta com
  `FakeDjenServer` (DJEN de mentira no formato real) → `DjenClient` real →
  handler real → Postgres real via Testcontainers: paginação, `MaxPages`,
  idempotência em duas execuções, multi-tenant, publicação inválida no meio
  da página, CNJ caindo no meio da execução, job desabilitado.

O transporte HTTP é o único dublê — nada depende de rede nem da
disponibilidade do CNJ.

## Riscos conhecidos

> Os números que sustentam estas mitigações foram **medidos** na Etapa 1.3 —
> ver [`../../poc/cnj-poc-report.md`](../../poc/cnj-poc-report.md) e
> [`../../integrations/cnj.md`](../../integrations/cnj.md). Em particular: o rate
> limit é de 20 requisições por janela de ~5 s, e o `count` da API não é
> confiável com `itensPorPagina=100`.

| Risco | Mitigação atual |
|---|---|
| Instabilidade do CNJ | retry com jitter, circuit breaker, job idempotente, health check `Degraded` |
| Rate limit não documentado | `429` mapeado e retentado com backoff; `PageSize` no máximo da API para reduzir número de chamadas |
| Volume de dados | índices parciais e BRIN (ADR-057), autovacuum agressivo; `MaxPages` limita cada execução |
| Mudança no contrato da API | item malformado é descartado com aviso em vez de derrubar a captura; métrica `invalid` sobe e alerta |
| `id` do CNJ renumerado | `hash` da origem preservado em `metadata_json` para reconciliação |
