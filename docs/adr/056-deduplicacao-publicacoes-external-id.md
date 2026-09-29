# ADR-056: Deduplicação de Publicações por ExternalId

**Status:** Aceito — implementado na Etapa 1.2

## Contexto

A captura de publicações do DJEN/CNJ é um job recorrente que consulta uma
**janela de datas**, não um cursor. A mesma publicação chega mais de uma vez
por vários motivos, todos normais:

- A janela de busca é deliberadamente sobreposta entre execuções
  (`LookbackDays = 3`), porque tribunais publicam com atraso: buscar apenas
  "ontem" perderia o que entrou no diário com dois dias de defasagem.
- O Hangfire faz retry de uma execução que falhou no meio — o que já foi
  persistido antes da falha será visto de novo na retentativa.
- Uma recarga histórica manual cobre períodos já capturados.
- O próprio CNJ ocasionalmente repete itens entre páginas de uma mesma
  consulta ampla.

Sem deduplicação, cada execução multiplicaria as publicações. Em um sistema
jurídico isso não é apenas ruído: a fila de revisão humana obrigatória (ver
ADR-012) ficaria cheia de duplicatas, o advogado revisaria a mesma intimação
várias vezes, e a confiança na automação — o diferencial do produto — cairia
por terra.

## Decisão

Usar o **`ExternalId`** (o `id` da comunicação na API do CNJ) como chave de
deduplicação, com unicidade garantida em **duas camadas**:

1. **Checagem em código**, no `CapturePublicationHandler`: se já existe
   publicação com aquele `(TenantId, ExternalId, Source)`, o comando retorna
   **sucesso** com `Existing = true`. Recapturar é operação normal, não erro.
   O job complementa isso com um pré-filtro de página inteira
   (`FindExistingExternalIdsAsync`), em uma única consulta, porque numa
   reexecução a maioria da página já existe.

2. **Índice único composto no banco**, que é a camada definitiva:

   ```sql
   ux_publications_tenant_external_id
     UNIQUE (tenant_id, external_id, source) WHERE is_deleted = false
   ```

Três decisões dentro da chave:

- **`tenant_id` faz parte da chave.** A mesma comunicação do CNJ pode
  legitimamente interessar a dois escritórios (partes ou advogados distintos
  no mesmo processo). Cada um tem a sua linha, com o seu ciclo de revisão
  independente. Unicidade global por `external_id` faria o segundo
  escritório nunca receber a publicação.

- **`source` faz parte da chave.** Hoje só existe o DJEN, mas o `id` é um
  inteiro sequencial da API do CNJ — nada garante que um diário futuro não
  reutilize o mesmo valor. Incluir a origem agora evita uma migração
  desagradável depois.

- **Filtro `WHERE is_deleted = false`.** Uma publicação excluída logicamente
  (LGPD, erro de captura) não deve impedir que a mesma comunicação seja
  recapturada.

Não usamos hash do conteúdo como chave: o CNJ reapresenta o mesmo ato com
pequenas diferenças de formatação entre consultas, e um hash instável
produziria exatamente as duplicatas que se quer evitar. O `hash` que o CNJ
devolve é preservado em `metadata_json`, para auditoria.

## Consequências

**Positivas:**

- A captura é **idempotente**: executar o job duas vezes sobre o mesmo
  período é seguro, o que torna o retry do Hangfire trivialmente correto e
  permite recarga histórica sem medo.
- A janela sobreposta deixa de ser um risco e passa a ser uma proteção
  contra publicação atrasada.
- O índice único segura **execuções concorrentes** do job, que a checagem em
  código sozinha não seguraria (há janela entre o `SELECT` e o `INSERT`).
- Verificado por testes de integração reais: duplicata dentro do tenant é
  rejeitada pelo banco; o mesmo `external_id` em outro tenant é aceito; uma
  segunda execução completa do job não persiste nada novo.

**Negativas / trade-offs:**

- Dependemos da estabilidade do `id` do CNJ. Se a API renumerar
  comunicações, publicações já capturadas voltariam como novas. Mitigação:
  o `hash` do CNJ fica em `metadata_json`, permitindo detectar e reconciliar
  o caso depois, sem perder dado.
- Sob concorrência, a violação do índice sobe como exception de
  infraestrutura (`23505`) em vez de resultado de negócio. Isso é aceitável
  porque o Hangfire retenta e a retentativa cai no caminho de duplicata —
  mas é um erro que aparece nos logs, e alguém pode interpretá-lo como falha
  real.
- O índice parcial (`WHERE is_deleted = false`) permite que a mesma
  comunicação exista duas vezes na tabela quando a primeira foi excluída
  logicamente. É o comportamento desejado, mas qualquer consulta que
  ignorar o filtro de soft delete verá as duas.
