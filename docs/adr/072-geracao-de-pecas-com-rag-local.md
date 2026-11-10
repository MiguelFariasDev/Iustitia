# ADR-072 — Geração assistida de peças com RAG local (Ollama + Qdrant)

**Status:** proposto — **depende de aprovação do responsável pela conformidade (LGPD/OAB) antes de
habilitar com dado de cliente** · **Contexto:** módulo `Drafting`

## Contexto

Pediu-se um módulo que, ao clicar em "Criar peça com IA", recupera trechos relevantes num banco
vetorial (Qdrant, embeddings BGE-M3), monta um prompt controlado, gera a minuta num modelo
`qwen3:8b` servido por Ollama numa instância alugada na Vast.ai, devolve o texto em streaming com
as fontes e persiste tudo.

Isto **diverge de decisões já tomadas** no projeto, que o dono do produto precisa aceitar de forma
explícita:

| Decisão anterior | O que muda aqui |
|---|---|
| `claude.md` §2/§15: provedor de IA é a Anthropic, com contrato de não-treinamento documentado; "nunca enviar dado de cliente a terceiro sem verificar o contrato" | Os **fatos do caso** (dado sigiloso de cliente) vão para um host de GPU alugado num marketplace (Vast.ai), sem contrato equivalente |
| Princípio 4 (sigilo profissional) e 3 (LGPD: criptografia em trânsito) | A porta do Ollama/Qdrant numa máquina de terceiros fica exposta; sem TLS, o texto trafega aberto |
| ADR-071: prompts versionados em `.md` embarcado | O system prompt é fixo e foi ditado literalmente; fica como constante de código, versionada (`SystemPrompt.Versao`) e travada por teste |

## Decisão

1. **Módulo novo `Drafting`** (Domain/Application/Infrastructure/Api, namespaces `Advocacia.Modules.Drafting.*`),
   com `DraftingDbContext` próprio e RLS (`016_drafting.sql`).
2. **Portas trocáveis** (`ILlmService`, `IEmbeddingService`, `IVectorStore`): trocar de modelo,
   provedor ou banco vetorial é registrar outra implementação em `DependencyInjection`. O Ollama
   implementa as duas primeiras (`OllamaService`); o Qdrant, a terceira (`QdrantService`, cliente
   oficial `Qdrant.Client`, gRPC).
3. **Desligado por padrão** (`Drafting:Enabled=false`; ligado só em `appsettings.Development.json`,
   contra `localhost`). Ligar em produção é a decisão consciente que esta ADR exige.
4. **Transporte seguro por construção** (`TransportGuard`): `http://` para host que não seja
   loopback é **recusado** a menos que `AllowInsecureHttp=true`; chave de API sob transporte
   inseguro é recusada **sempre**. O placeholder do `appsettings.json` (`IP_PUBLICO_DA_VAST:…`) não
   é uma URL válida: sem sobrescrever por variável de ambiente, a geração falha alto em vez de
   tentar uma conexão.
5. **Isolamento entre escritórios no Qdrant:** o Qdrant não tem RLS. Todo ponto leva `tenantId` no
   payload e **toda** busca e exclusão passa por `QdrantService.TenantFilter` — não há caminho de
   leitura sem ele (teste `QdrantServiceTests`). O payload pedido na especificação não tinha
   `tenantId`; foi acrescentado por isso.
6. **Prompt:** system prompt literal e fixo; texto do usuário e trechos recuperados só entram no
   user prompt, nos blocos pedidos. Defesas: limite de tamanho por campo, sanitização (caracteres
   de controle/invisíveis, rótulos de seção forjados), filtro de padrões de injeção
   (`PromptInjectionGuard`). **O filtro é um obstáculo, não uma garantia**; a defesa real é
   estrutural: o modelo não tem ferramentas, não vê dado fora do contexto e toda minuta passa por
   revisão humana.
7. **Verificação depois da geração** (`CitationVerifier`): as citações `[ARQUIVO, p. X]` existem
   entre os trechos recuperados? Há artigo/súmula no texto sem respaldo nas fontes nem nos dados do
   caso? A minuta traz as seções finais? Afirma "pronta para protocolo"? O resultado vira `avisos`
   no evento final. É checagem determinística — **não prova que a minuta está certa**.
8. **Streaming NDJSON** (`application/x-ndjson`): validação e criação da solicitação acontecem
   **antes** de abrir o stream (erro de entrada = HTTP 4xx `ProblemDetails`); depois de aberto, uma
   falha só pode ser o evento `{"tipo":"erro"}`. Cliente que desconecta cancela a geração.
9. **Auditoria só de metadados:** `legal_document_events` (e o log estruturado) guardam contagens,
   tempos e ids — nunca fatos, pedidos, trechos ou texto gerado. O texto fica em
   `legal_document_requests` (sob RLS), que é o registro de uso exigido.
10. **Versionamento:** regerar cria nova solicitação (`versao + 1`, `request_origem_id`); nada é
    sobrescrito.

## Detalhes que custam caro se esquecidos

- **`num_ctx`:** o Ollama usa 4096 por padrão e **trunca em silêncio o começo do prompt** (o system
  prompt!) quando estoura. Com ~8.000 tokens de contexto, `Ollama:NumCtx` está em 16384.
- **`think:false` + `ThinkTagFilter`:** o qwen3 pode emitir `<think>…</think>`; pedimos que não
  pense e ainda removemos o bloco, inclusive com a tag partida entre fragmentos.
- **Tokens do contexto são estimados** (≈ 3,5 caracteres/token): o tokenizador real não está
  disponível. A estimativa é propositalmente conservadora.
- **Porta do Qdrant:** o cliente oficial fala **gRPC** (6334), não REST (6333).
- **Sem retry** nas chamadas de geração: repetir um POST de minutos dobraria o custo de GPU.

## Consequências

- (+) Nenhuma dependência paga; modelo, provedor e banco vetorial substituíveis.
- (+) Entrada, saída e uso auditáveis; falhas e cancelamentos ficam registrados.
- (−) **Risco de sigilo:** a instância na Vast.ai é de terceiro. Mitigações obrigatórias no
  checklist de `docs/modules/drafting.md`. Sem elas, não habilitar com dado real.
- (−) `qwen3:8b` é um modelo pequeno: a minuta é rascunho e a revisão humana é obrigatória
  (Provimento CFOAB nº 271/2025 — princípio 1 do projeto).
- (−) O código deste módulo foi escrito **sem compilar nem rodar** (ambiente sem SDK .NET); ver
  "Estado da verificação" em `docs/modules/drafting.md`.

## Pendências

- Migração EF (`dotnet ef migrations add InitialDrafting`, comando no cabeçalho de `016_drafting.sql`).
- Criptografia de campo para `fatos`/`pedidos`/`texto_gerado` (ADR-068), hoje só criptografia em
  repouso do disco do Postgres.
- Integração do frontend (botão "Criar peça com IA" → NDJSON) e do aviso na interface.
- Origem do texto para indexação (extração de PDF/OCR do módulo Documents): `POST /pecas/indexar`
  recebe o texto já extraído.
