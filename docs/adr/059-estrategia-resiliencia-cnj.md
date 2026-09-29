# ADR-059: Estratégia de Resiliência para o CNJ

**Status:** Aceito — implementado na Etapa 1.3, com base em medição

Substitui as suposições da Etapa 1.1 sobre o comportamento do DJEN por números
medidos contra a API real (ver `docs/poc/cnj-poc-metrics.md`).

## Contexto

O DJEN é um serviço público, gratuito, sem contrato de nível de serviço, sem
documentação de limites e sem canal de aviso de mudanças. É, ao mesmo tempo, a
única fonte das publicações — o insumo de todo o produto.

A Etapa 1.1 implementou retry e circuit breaker por princípio, sem medir nada.
A prova de conceito (Etapa 1.3) mediu, e o que apareceu muda o desenho:

| Característica | Medido |
|---|---|
| Latência P50 / P95 / máx (sequencial) | 0,465 s / 1,827 s / 6,406 s |
| Taxa de sucesso (40 req. sequenciais) | 100% |
| **Rate limit** | **20 requisições, `429` na 21ª** |
| Recuperação após `429` | ≤ 5 s |
| Rajada concorrente (10 simultâneas) | 40 de 60 viraram `429` |
| `Retry-After` no `429` | **ausente** |
| Autenticação | não exigida |

O rate limit é a descoberta que importa: uma execução de produção como a
configurada (`MaxPages=50` × 4 tribunais = 200 requisições) tomaria `429` a
partir da 21ª e passaria o resto da execução em retry.

## Decisão

Resiliência em **quatro camadas**, de fora para dentro, cada uma resolvendo um
problema distinto:

### 1. Throttling local, antes de tudo (`CnjRateLimitingHandler`) — novo

`SlidingWindowRateLimiter` com **18 permissões por janela de 5 s**, segmentada
por segundo. Requisições excedentes **esperam na fila** em vez de irem à rede.

O ponto não é evitar o `429` por elegância: **cada `429` consome uma das 3
tentativas de retry daquela requisição**. Sem throttling, uma varredura longa
falha de verdade por ter gasto o orçamento de retry com rate limit em vez de com
instabilidade real. Tratar é pior que evitar.

18 e não 20: margem para o relógio do servidor não coincidir com o nosso.

### 2. Execução única entre instâncias (`DisableConcurrentExecution`) — novo

Lock distribuído no storage do Hangfire (PostgreSQL), válido **entre instâncias**.

O rate limit do CNJ é por origem, não por processo — duas instâncias do Worker
dividiriam o mesmo orçamento e ambas tomariam `429`. Além disso, a PoC observou
duas execuções simultâneas colidindo no índice único de deduplicação
(`23505`): o índice protegeu o dado, mas a execução terminou em falha inútil.

### 3. Retry com backoff exponencial e jitter (Polly 8)

3 tentativas, base 2 s, **com jitter**. Sem jitter, as tentativas de tribunais
diferentes sincronizariam em rajadas contra uma API já instável — precisamente o
padrão que a medição de rajada mostrou disparar `429` em massa.

Transiente = `408`, `429`, `5xx`, falha de rede e timeout. **`4xx` (exceto
408/429) não é retentado**: é erro nosso, e repetir só gasta orçamento.

### 4. Circuit breaker + timeout

Abre com 50% de falhas na janela de amostragem, fecha após 60 s. Timeout de 30 s
por tentativa — folgado em relação ao P95 de 1,8 s, mas o máximo observado foi
6,4 s e o serviço tem cauda longa.

### Fora do pipeline: agendamento

Cron diário às **03:00**, horário de menor uso do Judiciário. A janela de busca
é deliberadamente **sobreposta** (`LookbackDays = 3`), o que só é possível porque
a captura é idempotente (ADR-056) — instabilidade em uma execução é absorvida
pela seguinte, sem intervenção.

## Consequências

**Positivas:**

- A vazão fica abaixo do limite do CNJ **por construção**, não por sorte.
- O orçamento de retry passa a ser gasto apenas com falhas reais.
- Cada camada resolve um problema diferente e é ajustável isoladamente por
  `appsettings` (seção `Cnj`), sem recompilar.

**Negativas / trade-offs:**

- **Teto de vazão de ~3,6 req/s.** Uma varredura de 200 páginas leva no mínimo
  ~56 s só de throttling. Aceitável para um job noturno; não seria para captura
  sob demanda com usuário esperando.
- **O limitador é por processo.** `DisableConcurrentExecution` garante uma
  execução por vez do job, o que hoje é suficiente; mas se a captura for um dia
  paralelizada dentro do processo ou dividida entre instâncias por tribunal, o
  limite precisa virar distribuído (Redis). Está documentado no código.
- **Os números são de uma amostra.** Medição de um ponto de rede, em um horário,
  contra um serviço público variável. O rate limit foi reprodutível em três
  execuções independentes; a latência não deve ser lida como SLA.
- **Sem `Retry-After`**, o backoff é um palpite calibrado pela recuperação
  observada (≤ 5 s), não uma instrução do servidor.

## Riscos não mitigáveis por código

- **Mudança de contrato sem aviso.** Mitigação parcial: item malformado é
  descartado com aviso em vez de derrubar a captura, e a métrica
  `cnj.capture.invalid.count` sobe — o que transforma uma mudança de contrato em
  alerta, não em silêncio.
- **Aperto do rate limit ou restrição de acesso.** O CNJ pode mudar as regras a
  qualquer momento. Não há contrato. Este é um risco de produto, não de
  engenharia, e precisa estar visível para quem decide.
- **Indisponibilidade prolongada.** A captura atrasa; os prazos não. É a razão
  pela qual a revisão humana (ADR-012) nunca pode depender só do que o sistema
  capturou.
