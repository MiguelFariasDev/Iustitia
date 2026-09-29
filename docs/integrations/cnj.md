# Integração com o CNJ (DJEN)

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
