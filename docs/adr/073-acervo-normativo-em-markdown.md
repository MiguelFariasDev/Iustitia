# ADR-073 — Acervo normativo em Markdown (ctresb/leis) com busca híbrida

Status: proposto (código não compilado nem executado — ver "Estado da verificação" em `docs/modules/drafting.md`).

## Contexto
O RAG do ADR-072 só enxergava documentos enviados pelo escritório. Para minutas fundamentadas é preciso um
acervo normativo (Constituição, leis, súmulas, decisões, tratados, atos regulatórios). O repositório
`ctresb/leis` publica esse acervo já convertido em Markdown com cabeçalho YAML, mas o repositório inteiro é
enorme (centenas de milhares de arquivos de auditoria, `_fonte/` com originais HTML/PDF e um `.git` pesado).

## Decisão
1. **Markdown é a fonte primária.** Nada de OCR ou conversão de PDF para o acervo normativo (OCR só em
   documentos próprios do escritório, como antes).
2. **Download seletivo:** `git init` + `fetch --depth 1 --filter=blob:none` + sparse-checkout em modo "não cone".
   Os padrões incluem as pastas configuradas e **excluem** `_*` (auditorias, lotes, índices, `_fonte`), `scripts/`
   e tudo que não é Markdown dentro das pastas. O `.git` nunca é clonado com histórico. Testado contra o
   repositório real (commit `c9f21b4`): 6 s de fetch, 7 s de checkout, só `.md`.
3. **Três camadas:** object storage (Markdown integral e versões antigas) · PostgreSQL (norma, versão, chunks,
   jobs, texto para full-text) · Qdrant (vetor + payload + texto do chunk). Nunca o Markdown inteiro no Qdrant.
4. **Incremental por SHA-256** do arquivo. Novo/alterado → reprocessa; igual → nada (nem lê a GPU); sumiu →
   some da busca, histórico e Markdown ficam. A GPU só liga se houver o que embutir.
5. **Chunking jurídico hierárquico** (artigo → § → inciso → alínea → parágrafo → frase), 400–900 tokens,
   com título, ementa curta, hierarquia e referência em todo chunk.
6. **Sete coleções públicas** + `documentos_cliente` (privada, por tenant) + `modelos_de_peca` (reservada).
7. **Busca híbrida obrigatória:** vetor (Qdrant) + palavra-chave (full-text do PostgreSQL), fusão RRF,
   deduplicação, limite por norma, reranking por regras, 5–10 trechos ao LLM.
8. **Validação pós-geração** de cada citação (Validada, PendenteDeVerificacao, PossivelAlucinacao,
   ConflitoDeVersao, NormaRevogada) e **trava de exportação** enquanto houver pendência ou possível invenção.
9. **System prompt v2** (15 regras): acrescenta o formato `[NORMA, art. X]` e as regras de norma no contexto,
   conflito de versões e revogação.

## Correções às premissas do pedido (verificadas no repositório real)
- **`municipais/CE/Fortaleza` não existe:** o diretório é `municipais/CE/fortaleza` (minúsculo; Linux e o
  sparse-checkout diferenciam caixa). Com o nome do pedido o clone parcial viria vazio.
- **Incluir `federal/` inteiro traz lixo:** há ~100 mil Markdowns de auditoria em `federal/_fonte/` e milhares de
  JSON `federal/_*.json`. Por isso a exclusão de qualquer segmento iniciado por `_` é regra, não detalhe.
- **O YAML não tem os campos que o pedido lista:** não existem `situacao` (só em alguns), `publicacao`, `area`
  nem `assuntos`; `urn` falta fora das normas federais; `revogada` é `null` em municipais; `data` vem vazia em
  tratados (que usam `situacao_indice`); a Constituição não tem YAML. O resolvedor completa o que dá por
  caminho/conteúdo e **não inventa** o resto (vigência fica "Não informada").
- **PostgreSQL não tem BM25:** o full-text (`tsvector` 'portuguese' + `ts_rank_cd`) cumpre o papel de segundo
  sinal, mas não é BM25. BM25 real exige extensão (pg_search/ParadeDB) ou o modo esparso do Qdrant.
- **`reguladores/` tem ~273 mil Markdowns** (RFB 75 mil, Anvisa 26 mil, MS 24 mil…): é a maior fatia do acervo e a
  de menor valor para minutas. Está incluída como pedido, mas a ordem de prioridade a deixa por último e o
  limite por ciclo faz a primeira carga avançar em fatias. Recomenda-se restringir (`PastasIncluidas`) na fase 1.
- **O "reranker" não é um cross-encoder:** o Ollama não expõe endpoint de reranking. É reranking por regras
  (determinístico, sem custo de GPU), com interface para trocar depois.

## Alternativas rejeitadas
Clonar tudo (140 GB, histórico inútil); converter PDFs/HTML de `_fonte/` (retrabalho do que já está em
Markdown); uma coleção gigante (filtros e quantização por tipo ficam piores); só busca vetorial (perde número de
lei/artigo citado); AWSSDK.S3 (dependência grande e problemas de checksum com R2/B2 para 4 operações — foi
escrito um cliente SigV4 mínimo).

## Consequências e riscos
- O acervo é **informativo**: o próprio repositório avisa que deriva de conversão automática/OCR e não substitui
  o Diário Oficial. Toda citação carrega fonte oficial, arquivo, versão e vigência; a minuta continua exigindo
  revisão humana.
- A primeira carga é longa (centenas de milhares de arquivos) e usa a GPU de forma contínua; o custo cresce com
  o escopo. O worker avança por prioridade e em fatias; ver estimativas em `docs/modules/drafting.md`.
- Vigência é a informada pela fonte (`revogada`, `situacao`); não há motor de vigência por data.
- Mudar o chunking ou o modelo de embedding exige reprocessamento (`?reprocessar=true`).
- Código não compilado/executado; migração EF não gerada (o repositório usa SQL em `infra/supabase/migrations`).
