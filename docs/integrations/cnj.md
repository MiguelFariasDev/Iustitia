# Integrações com o CNJ

O CNJ expõe **duas** APIs públicas, e o Iustitia usa as duas — para coisas diferentes.
Confundi-las é fácil e caro, então a distinção vem antes de tudo:

| | DJEN / Comunica | DataJud |
|---|---|---|
| Base | `comunicaapi.pje.jus.br` | `api-publica.datajud.cnj.jus.br` |
| Traz | **Texto** das publicações do diário | **Metadados** do processo |
| Autenticação | nenhuma | `Authorization: APIKey …` |
| Formato | REST, query string | Elasticsearch Query DSL (POST) |
| Quem consome | `CnjCaptureJob` (Worker, diário) | Formulário de cadastro (sob demanda) |
| Porta | `ICnjClient` | `IConsultaProcessoTribunal` (módulo Legal) |
| Uso comercial | ver termo próprio | **proibido** — ver ADR-064 |

Este documento cobre as duas seções abaixo.

---

# DJEN / Comunica — publicações

API pública de comunicações processuais do Conselho Nacional de Justiça, fonte
das publicações capturadas pelo Iustitia.

- Base: `https://comunicaapi.pje.jus.br/`
- Recurso: `GET /api/v1/comunicacao`
- **Autenticação: não exige.** Não há cadastro, chave nem token.
- Implementação: `Integrations.CNJ.Infrastructure/Djen/DjenClient.cs`
- Porta consumida pelos módulos: `ICnjClient` (Integrations.CNJ.Application)

O fluxo de captura está em [`../modules/legal/cnj-capture.md`](../modules/legal/cnj-capture.md).
A estratégia de resiliência, com os números que a sustentam, está na **ADR-059**.

## Parâmetros de consulta

| Parâmetro | Uso |
|---|---|
| `dataDisponibilizacaoInicio` / `Fim` | `yyyy-MM-dd`. Varredura do diário por período. |
| `siglaTribunal` | Ex.: `TJSP`. **Omitir devolve o diário nacional inteiro** — o cliente recusa consulta sem nenhum critério. |
| `numeroProcesso` | **Sem máscara.** Devolve o histórico do processo; dispensa intervalo de datas. |
| `pagina` | 1-based. |
| `itensPorPagina` | Máximo 100. Ver a armadilha do `count` abaixo. |

## Comportamento observado (prova de conceito — Etapa 1.3)

Medido contra a API real em 28/09/2026. Números completos em
[`../poc/cnj-poc-metrics.md`](../poc/cnj-poc-metrics.md); o relatório executivo
está em [`../poc/cnj-poc-report.md`](../poc/cnj-poc-report.md).

### Limitações confirmadas

| Limitação | Valor medido | Como o sistema lida |
|---|---|---|
| **Rate limit** | 20 requisições, `429` na 21ª; recupera em ≤ 5 s | `CnjRateLimitingHandler` (18 req / janela de 5 s) |
| `Retry-After` no `429` | **ausente** | backoff exponencial calibrado pela recuperação observada |
| Latência P50 / P95 / máx | 0,465 s / 1,827 s / 6,406 s | timeout de 30 s por tentativa |
| Máximo de itens por página | 100 | `MaxPageSize` |
| Rajada concorrente | 10 simultâneas ⇒ 67% de `429` | acesso serializado pelo limitador |
| Disponibilidade na janela medida | 100% (100 requisições, zero `5xx`) | circuit breaker segue necessário — amostra curta |

### Armadilha: o `count` mente com `itensPorPagina=100`

**A mesma consulta, variando só o tamanho da página:**

| `itensPorPagina` | `count` devolvido | Itens recebidos |
|---|---|---|
| 10 | 14.138 | 10 |
| 50 | 14.138 | 50 |
| **100** | **100** ← errado | 100 |

Com o tamanho máximo de página, o DJEN repete o tamanho da página no lugar do
total. Qualquer paginação que confie em `count` **para na primeira página e
perde o resto sem erro, sem log e sem métrica**.

Foi exatamente o que aconteceu com a implementação da Etapa 1.2: uma consulta com
14.138 publicações disponíveis capturava 100. Ver `CnjPublicationPage.HasMorePages`:
a regra passou a ser **página cheia sempre manda continuar**, e o `count` só pode
adicionar páginas, nunca encerrar a varredura antes de uma página incompleta.

### Itens malformados

Uma fração dos itens chega sem `id`, sem número de processo, sem texto ou sem
data. O cliente descarta esses itens com aviso agregado em vez de falhar a
página — um item ruim não pode custar as outras centenas. A métrica
`cnj.capture.invalid.count` transforma uma eventual mudança de contrato em
alerta.

### Qualidade dos dados

Em 43 publicações reais de 7 processos e 4 tribunais, após a normalização do
`RawContent`: zero tags HTML residuais, zero entidades não decodificadas, zero
espaços duplicados, zero problemas de encoding (acentuação preservada em 42 das
43), zero truncamentos, 100% dos metadados de origem presentes.

## Recomendações para produção

1. **Manter o throttling ligado.** É o que impede a captura de gastar o
   orçamento de retry com rate limit auto-infligido.
2. **Não escalar o Worker horizontalmente sem rever o limite.** Ele é por
   processo; o limite do CNJ é por origem. Hoje `DisableConcurrentExecution`
   resolve — se a captura for paralelizada, o limite precisa virar distribuído
   (Redis).
3. **Preferir `itensPorPagina=100`** (menos requisições, e o limite é por
   requisição, não por item) — seguro agora que a paginação não depende do
   `count`.
4. **Usar a captura por processo para os processos cadastrados** e a varredura
   por tribunal só para descobrir o que ainda não está cadastrado. A consulta
   dirigida é mais barata, mais precisa e traz o histórico completo.
5. **Alertar sobre `cnj.capture.invalid.count`**, não só sobre erros. Subida
   repentina é o primeiro sinal de mudança de contrato.
6. **Rodar às 03:00**, com janela sobreposta (`LookbackDays ≥ 3`): tribunais
   publicam com atraso, e a deduplicação torna a sobreposição gratuita.
7. **Tratar o CNJ como fonte não confiável por contrato.** Não há SLA. A revisão
   humana obrigatória (ADR-012) não pode depender de a captura estar completa.

## Riscos

| Risco | Natureza |
|---|---|
| Mudança de contrato sem aviso | alto — sem canal de comunicação; mitigado por descarte tolerante + alerta |
| Aperto do rate limit | médio — não há contrato; exigiria reduzir vazão |
| Restrição/remoção do acesso público | **risco de produto, não de engenharia** — precisa estar visível para quem decide |
| Indisponibilidade prolongada | médio — a captura atrasa, os prazos não |

---

# DataJud — metadados do processo

Base de metadados processuais que os tribunais enviam ao CNJ. Usada para auto-preencher o
cadastro de processos.

> **USO RESTRITO A PORTFÓLIO.** O Termo de Uso do DataJud proíbe uso comercial (cláusulas
> 3.3 e 3.8) e consumir a API implica aceitá-lo. Ver
> [`../compliance/datajud-termo.md`](../compliance/datajud-termo.md) e a **ADR-064**.

- Base: `https://api-publica.datajud.cnj.jus.br`
- Recurso: `POST /api_publica_{alias}/_search`
- **Autenticação:** `Authorization: APIKey <chave>` — a chave é pública (divulgada na wiki do
  CNJ), mas fica em user-secrets/Key Vault, nunca no appsettings versionado: o CNJ pode
  trocá-la sem aviso, e trocá-la deve ser um comando, não um commit.
- Implementação: `Integrations.CNJ.Infrastructure/DataJud/DataJudClient.cs`
- Porta consumida pelo módulo Legal: `IConsultaProcessoTribunal`

## Índice por tribunal

Cada tribunal tem o seu índice: `/api_publica_tjsp/_search`, `/api_publica_trf1/_search`.
O alias é a sigla em minúsculas, e a lista é **explícita** (`DataJudEndpoints`) — derivar por
minúsculas geraria uma URL plausível para um índice inexistente, e o erro chegaria como 404
genérico em vez de "tribunal não suportado".

**O STF não publica no DataJud.** A tela desabilita o botão de consulta nesse caso, em vez de
gastar a chamada.

O tribunal é derivado do **próprio número CNJ** (`TribunalResolver`, campos J e TR da
Resolução 65/2008). Nunca é informado pelo usuário: divergir número e tribunal devolve "não
encontrado" sem erro nenhum que denuncie a causa.

## Requisição

```json
POST /api_publica_trf1/_search
{ "query": { "match": { "numeroProcesso": "00008323520184013202" } }, "size": 1 }
```

O número vai **sem máscara**, 20 dígitos — mesma regra do DJEN.

## Comportamento observado (setembro de 2026)

Medido contra a API real:

| | Observado | Documentado |
|---|---|---|
| Latência da 1ª consulta | **~12 a 14 s** | ~500 ms (P50) |
| Consulta em cache (nossa) | ~40 ms | — |
| `dataAjuizamento` | `"20181029000000"` | ISO 8601 |
| `movimentos[].dataHora` | `"2018-10-30T14:06:24.000Z"` | ISO 8601 |

### A armadilha das datas

**Os dois formatos convivem na mesma resposta.** `dataAjuizamento` vem compacto
(`yyyyMMddHHmmss`) e `dataHora` vem ISO. A documentação mostra ISO nos dois.

Um cliente que só entenda ISO devolve erro de parsing sobre um HTTP 200 perfeitamente válido.
`DataJudDateTimeConverter` aceita os dois, e data ilegível vira `null` em vez de exceção —
perder a data degrada o auto-preenchimento; derrubar a consulta cancela o recurso.

É o mesmo padrão do `count` mentiroso do DJEN: documentação de API pública descreve a
intenção, não o que o serviço faz.

### Processo não encontrado

Responde **HTTP 200 com `hits.total.value = 0`**, não 404. Nem todo processo está lá — depende
do que o tribunal enviou. O endpoint devolve `found: false` com 200, para a tela distinguir
"não existe lá" de "a consulta falhou".

## Contenção

| Medida | Valor | Por quê |
|---|---|---|
| Cache (Redis) | 1 h por `(tribunal, processo)` | O CNJ pede que não se faça consulta em massa |
| Cache do "não encontrado" | sim | É o número mais reconsultado — o usuário acha que errou |
| Rate limit | 10/min por usuário | `RateLimitingConfiguration.ExternalQueryPolicy` |
| Circuit breaker | abre com 50% de falhas em 5 tentativas | Quem espera é uma pessoa no formulário, não um job |
| Timeout | 30 s por tentativa | A latência real exige folga |

## Erros

| Código | Quando |
|---|---|
| `PROCESS_CNJ_INVALID` | Número mal formado ou com dígito verificador errado |
| `CNJ_TRIBUNAL_NOT_SUPPORTED` | Tribunal fora do DataJud (ex.: STF) |
| `CNJ_AUTH_FAILED` | Chave inválida ou revogada — **verificar se o CNJ a trocou** |
| `CNJ_RATE_LIMITED` | Limite do CNJ excedido |
| `CNJ_UNAVAILABLE` / `CNJ_TIMEOUT` | Serviço fora do ar ou lento demais |
| `CNJ_INVALID_RESPONSE` | Resposta ilegível — provável mudança de contrato |
| `COMMON_RATE_LIMITED` | Nosso limite de 10/min por usuário |

Em todos, o cadastro manual continua disponível. Nenhum erro impede cadastrar o processo.

## Observabilidade

- `ActivitySource`: `Advocacia.CNJ.DataJud` — spans `datajud.consultar` e `datajud.parse`
- Métricas: `datajud.requests.count`, `.failed.count`, `.rate_limited.count`,
  `.not_found.count`, `datajud.cache.hits.count`, `datajud.requests.duration`

Nenhuma métrica ou log carrega o payload da resposta: os metadados incluem partes e
movimentos de um processo real, que pode estar em segredo de justiça. O que se registra é
tribunal, desfecho e latência.
