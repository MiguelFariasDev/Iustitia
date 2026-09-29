# Prova de Conceito — Captura de Publicações do CNJ

**Etapa 1.3 · FASE 1 · 28 de setembro de 2026**

Documentos de apoio: [métricas](cnj-poc-metrics.md) · [lições aprendidas](cnj-poc-lessons.md) ·
ADR-058 (método) · ADR-059 (resiliência)

---

## Sumário executivo

**Conclusão: APROVAR COM RESSALVAS.** A captura de publicações do DJEN/CNJ é
tecnicamente viável e está funcionando ponta a ponta contra a API real. A FASE 2
pode começar.

A prova de conceito **encontrou três defeitos que 920 testes automatizados não
encontraram**, um deles grave o bastante para inviabilizar o produto se tivesse
chegado à produção:

> Com o tamanho de página que usávamos, a API do CNJ devolve um total **errado**.
> Nossa paginação confiava nesse número e **parava na primeira página**. Uma
> consulta com 14.138 publicações disponíveis capturava 100 — **0,7%** — sem
> erro, sem log e sem alerta. Do ponto de vista de quem opera, parecia sucesso.

Os três defeitos foram corrigidos e cobertos por testes de regressão. Os números
que sustentam as decisões de arquitetura deixaram de ser expectativa e passaram a
ser medição.

As ressalvas não são técnicas — são de **dependência e de ambiente**, e estão na
seção [Ressalvas](#ressalvas).

---

## Objetivo

Validar, contra a API real, se a captura construída nas Etapas 1.1 e 1.2
funciona com processos reais — antes de investir nas FASES 2 a 9, que dependem
inteiramente dela.

## Metodologia

| Item | Escolha | Por quê |
|---|---|---|
| Processos | **7 processos reais e públicos**, obtidos do próprio DJEN | São dado público. Processo de cliente exigiria autorização expressa e traria dado sob sigilo para um ambiente de desenvolvimento, sem acrescentar nada à validação: a API não se comporta diferente conforme quem acompanha o processo. |
| Tribunais | TJCE, TJRJ, TJMG, TJSP | Cobrem 4 estados e os maiores volumes. |
| API | **Real** (`comunicaapi.pje.jus.br`) | É a fonte de risco desta fase. |
| Infraestrutura | Local (Docker) | Supabase/Azure não influenciam o que está sendo medido. |
| Volume p/ índices | 50.000 publicações sintéticas | Sem volume, `EXPLAIN ANALYZE` não diz nada: o planner escolhe seq scan corretamente em tabela pequena. |
| RLS | 2 tenants, conectando como `app_user` | O dono das tabelas bypassa RLS — um teste como dono passaria mesmo sem policy. |

---

## Resultados

### Captura por processo — o cenário do escritório

| Métrica | Resultado |
|---|---|
| Publicações esperadas | 43 |
| **Publicações capturadas** | **43 — recall de 100%** |
| Inválidas / erros | **0 / 0** |
| Duração (1ª execução) | 15,0 s |
| 2ª execução (1 min depois) | 0 novas, 43 duplicadas, 0,88 s |

A publicação mais antiga recuperada é de **março de 2023** — 3,5 anos fora de
qualquer janela de varredura. Confirma que a captura por processo deve ignorar a
janela de datas: um processo recém-cadastrado precisa do histórico anterior ao
cadastro.

### Varredura por tribunal — o cenário de descoberta

| Métrica | Resultado |
|---|---|
| Publicações persistidas | 1.800+ (execução interrompida por tempo, não por erro) |
| Processos distintos | ~1.500 |
| Vazão | **~10 publicações/s**, 0,09 req/s |
| Eventos de rate limit | **0** |

### Qualidade dos dados — 43 publicações reais

Zero tags HTML residuais, zero entidades não decodificadas, zero espaços
duplicados, zero encoding corrompido, zero truncamentos, 100% dos metadados de
origem presentes, 100% em `PendingReview`, zero datas no futuro.

**A validação de dígitos verificadores do número CNJ rejeitou 0 de 1.243
publicações reais** — era o risco nº 1 apontado no relatório da Etapa 1.2, e está
encerrado.

### Isolamento por tenant

| Cenário | Observado |
|---|---|
| Tenant A vê | só as suas 43 (de 50.044 na tabela) |
| Sem tenant na sessão | 0 linhas, sem erro (*fail closed*) |
| `INSERT` para outro tenant | **negado pelo Postgres** |

---

## Defeitos encontrados e corrigidos

### 1. Paginação truncada por `count` incorreto — **grave**

A mesma consulta, variando só o tamanho da página:

| `itensPorPagina` | `count` devolvido | Total real |
|---|---|---|
| 10 | 14.138 | 14.138 |
| 50 | 14.138 | 14.138 |
| **100** | **100** | **14.138** |

`HasMorePages` confiava no `count`. Correção: **página cheia sempre manda
continuar**; o `count` só pode adicionar páginas, nunca encerrar a varredura
antes de uma página incompleta. Coberto por teste de unidade e de integração, com
o dublê agora replicando o defeito real da API.

### 2. Rate limit não documentado — **médio**

**20 requisições, `429` na 21ª, recuperação em ≤ 5 s** — reprodutível em três
execuções. Sem `Retry-After`. Uma configuração de produção (50 páginas × 4
tribunais = 200 requisições) tomaria `429` a partir da 21ª.

Correção: throttling local (`CnjRateLimitingHandler`, 18 req/janela de 5 s) antes
do pipeline de resiliência. O motivo não é elegância: **cada `429` consome uma
das 3 tentativas de retry**, então sem throttling uma varredura longa falha de
verdade por ter gasto o orçamento com rate limit em vez de instabilidade real.

### 3. Índice parcial com a coluna errada — **médio**

O índice da fila de revisão estava chaveado por `(tenant_id, process_id)`, mas a
consulta ordena por data. Com 50 mil publicações: **25,0 ms → 0,18 ms (139×)** ao
trocar para `(tenant_id, published_at)`. Corrigido por migration; ADR-057
atualizada.

### 4. Execução concorrente entre instâncias — **baixo, observado por acidente**

Duas instâncias do Worker rodando ao mesmo tempo colidiram no índice único de
deduplicação (`23505`). O dado ficou íntegro — **a ADR-056 funcionou** — mas a
execução falhou sem necessidade. Corrigido com `DisableConcurrentExecution`
(lock distribuído no PostgreSQL, válido entre instâncias).

---

## Limitações do CNJ

| Limitação | Medido | Tratamento |
|---|---|---|
| Rate limit | 20 req / janela ~5 s | throttling local |
| `Retry-After` | ausente | backoff calibrado pela recuperação observada |
| `count` não confiável | com `itensPorPagina=100` | paginação não depende dele |
| Latência P50 / P95 / máx | 0,465 / 1,827 / 6,406 s | timeout de 30 s por tentativa |
| Itens por página | máx. 100 | respeitado pelo cliente |
| Itens malformados | fração pequena | descartados com aviso, sem falhar a página |
| Autenticação | não exige | — |
| Disponibilidade na janela medida | 100% | circuit breaker mantido — amostra curta |

---

## Riscos

| Risco | Severidade | Natureza |
|---|---|---|
| **Mudança de contrato sem aviso** | **Alta** | Não há canal de comunicação nem versionamento. O defeito do `count` mostra que o comportamento real já diverge do esperado. Mitigado parcialmente: item malformado vira métrica, não falha silenciosa. |
| **Restrição ou remoção do acesso público** | **Alta** | **Risco de produto, não de engenharia.** Não há contrato com o CNJ. Se o acesso for restringido, não há solução técnica. |
| Aperto do rate limit | Média | Exigiria reduzir vazão; o throttling já é configurável sem recompilar. |
| Indisponibilidade prolongada | Média | A captura atrasa; os prazos não. É a razão de a revisão humana nunca poder depender só do capturado. |
| Volume de dados | Baixa | 98 MB de tabela + 39 MB de índices por 50 mil publicações. Previsível e controlável. |
| Persistência como gargalo | Baixa | ~85 min para 4 tribunais/dia. Aceitável para job noturno; solução conhecida (escrita em lote). |

---

## Ressalvas

A aprovação é **com ressalvas** por três pendências que esta etapa não podia
resolver e que não devem ser tratadas como concluídas:

1. **Nenhum processo de cliente real do escritório foi usado.** Os 7 processos
   são públicos. A captura foi validada tecnicamente, mas não contra a carteira
   real — cujos tribunais, classes e volumes podem diferir.
2. **Não houve validação em staging real.** Supabase e Azure não foram
   provisionados. Ficam sem medição: autenticação ponta a ponta, traces no
   Application Insights, consumo de CPU/memória em Container App e comportamento
   do Supabase sob carga.
3. **A medição é uma amostra.** Um ponto de rede, algumas horas, um serviço
   público sem SLA. O rate limit foi reprodutível e pode ser tratado como
   característica; a latência, não.

---

## Recomendações para a FASE 2

**Antes de construir:**

1. Rodar a captura por **7 dias consecutivos**, com a carteira real do
   escritório, e comparar o capturado com o que os advogados efetivamente
   receberam. É o único teste que fecha a pergunta de recall.
2. Provisionar **Supabase staging** — é o bloqueador de tudo que envolve
   usuário, e nenhum fluxo de auth foi exercitado até hoje.

**Durante a FASE 2:**

3. **Métrica de cobertura** (capturado ÷ disponível na origem), com alerta. É a
   única que teria detectado o defeito do `count`.
4. **Persistência em lote** por página — potencial de 10× na varredura.
5. **Vincular publicação a processo na captura**, agora que existe cadastro e a
   consulta já é dirigida por número CNJ.
6. **Trocar `CnjCaptureJobOptions.CnjNumbers`** (andaime da PoC) pelo cadastro
   real de processos por tenant.

**Decisão de produto:**

7. O CNJ é dependência **única, gratuita e sem contrato**. Vale decidir
   conscientemente se o produto aceita esse risco ou se busca uma fonte
   redundante (diários estaduais, provedores comerciais) antes de escalar.

---

## Conclusão

**APROVAR COM RESSALVAS.**

A captura funciona, os dados chegam íntegros, o isolamento por tenant é
inviolável nos cenários testados e os índices sustentam as consultas do produto
em volume. A base técnica da FASE 2 está de pé.

A prova de conceito se pagou na primeira hora: o defeito da paginação teria
chegado à produção capturando 0,7% das publicações **e parecendo bem-sucedido**.
Em um sistema jurídico, esse é o pior modo de falha possível — uma publicação não
capturada não gera erro, não gera reclamação e não existe, até o prazo vencer.

As ressalvas são reais e devem acompanhar a decisão: nenhum processo de cliente
foi usado, nenhum ambiente de staging foi validado, e a dependência central do
produto é um serviço público sem contrato.
