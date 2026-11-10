# Módulo Drafting — geração assistida de peças com RAG local

Decisão e riscos: [ADR-072](../adr/072-geracao-de-pecas-com-rag-local.md). Leia a seção
"Estado da verificação" antes de confiar neste código.

## Fluxo

```
Frontend ──POST /api/v1/pecas/gerar (JWT)──▶ PecasEndpoints
   1. PrepareAsync   valida + sanitiza + grava solicitação (Pendente)         → erro aqui = HTTP 4xx
   2. RagService     embedding (BGE-M3/Ollama) → busca no Qdrant (filtro tenant+área) → dedupe → ≤ 8.000 tokens
   3. PromptBuilder  system prompt fixo + user prompt (fatos, pedidos, contexto)
   4. OllamaService  POST /api/chat em streaming NDJSON (qwen3:8b)
   5. NDJSON ao frontend: {"tipo":"fragmento",…}… {"tipo":"fim",fontes,avisos,aviso,tempoGeracaoMs,…}
   6. CitationVerifier + persistência (texto, fontes, modelo, tempo, eventos)
```

O frontend nunca recebe URL, porta, chave ou ponto do Qdrant, nem endereço do Ollama/Vast.ai.

## Estrutura de pastas

```
src/Modules/Drafting/
├── Drafting.Domain/Pecas/
│   ├── LegalDocumentRequest.cs      # agregado: solicitação + texto + versionamento
│   ├── RetrievedSource.cs           # trecho que entrou no prompt (e se foi citado)
│   ├── LegalDocumentEvent.cs        # trilha de auditoria (só metadados)
│   ├── LegalDocumentRequestId.cs · LegalDocumentStatus.cs
├── Drafting.Application/
│   ├── Abstractions/                # ILlmService, IEmbeddingService, IVectorStore, IRagService,
│   │                                # IPromptBuilder, ILegalDocumentService, IDraftingAuditService,
│   │                                # IPecaGenerationService, IDocumentIndexer, repositório/UoW
│   ├── Models/                      # GerarPecaRequest, eventos NDJSON, RAG (chunks, hits, query)
│   └── Services/
│       ├── PecaGenerationService.cs # orquestração (stream, falhas, cancelamento)
│       ├── RagService.cs · ContextSelector.cs · TokenEstimator.cs
│       ├── PromptBuilder.cs · SystemPrompt.cs
│       ├── InputSanitizer.cs · PromptInjectionGuard.cs · GerarPecaValidation.cs · PecaCatalogo.cs
│       ├── CitationVerifier.cs
│       ├── LegalDocumentService.cs · DraftingAuditService.cs · DocumentIndexer.cs
├── Drafting.Infrastructure/
│   ├── Ollama/   OllamaService.cs · OllamaOptions.cs (+TransportGuard) · ThinkTagFilter.cs
│   ├── Qdrant/   QdrantService.cs · QdrantOptions.cs
│   └── Persistence/  DraftingDbContext · Configurations/ · Repositories/ · DraftingUnitOfWork
└── Drafting.Api/  PecasEndpoints.cs · NdjsonStreamResult.cs
infra/supabase/migrations/016_drafting.sql        # RLS
tests/UnitTests/Drafting.{Application,Infrastructure,Api}.UnitTests/
```

## Endpoints

Versionados pelo padrão do projeto (`/api/v1/...`; a spec dizia `/api/pecas/gerar`).

| Método | Rota | Papel | Observação |
|---|---|---|---|
| POST | `/api/v1/pecas/gerar` | Owner/Partner/Lawyer | 6 por minuto por usuário (`ai-generation`) |
| POST | `/api/v1/pecas/indexar` | Owner/Partner | Indexa o **texto já extraído** de um documento (substitui a indexação anterior) |

Request de `gerar` (`documentosIds` são **GUIDs** do módulo Documents, não inteiros):

```json
{
  "tipoPeca": "peticao_inicial",
  "area": "civel",
  "fatos": "…",
  "pedidos": "…",
  "documentosIds": ["3f2b…"],
  "instrucoesAdicionais": null,
  "stream": true,
  "pecaOrigemId": null
}
```

`tipoPeca`: `peticao_inicial`, `contestacao`, `replica`, `recurso_apelacao`, `embargos_declaracao`,
`agravo_instrumento`, `contrarrazoes`, `manifestacao`. `area`: `civel`, `trabalhista`,
`previdenciario`, `tributario`, `familia`, `consumidor`, `administrativo`, `empresarial`.
Limites: fatos 8.000 · pedidos 4.000 · instruções 2.000 caracteres · 20 documentos.

### Exemplo (curl)

```bash
# --no-buffer: sem isso o curl segura o NDJSON e você só vê o texto no final.
curl --no-buffer -X POST "https://api.exemplo.com.br/api/v1/pecas/gerar" \
  -H "Authorization: Bearer $JWT" \
  -H "Content-Type: application/json" \
  -d '{
    "tipoPeca": "peticao_inicial",
    "area": "civel",
    "fatos": "O autor contratou o serviço X em 10/03/2025 e pagou R$ 3.000,00 antecipadamente; o serviço não foi prestado.",
    "pedidos": "Restituição do valor pago e indenização por danos morais.",
    "stream": true
  }'
```

Saída (uma linha de JSON por evento):

```
{"tipo":"fragmento","conteudo":"ENDEREÇAMENTO\nExcelentíssimo…"}
{"tipo":"fragmento","conteudo":" Senhor Doutor Juiz…"}
{"tipo":"fim","solicitacaoId":"…","versao":1,"fontes":[{"nomeArquivo":"contrato.pdf","pagina":3,"score":0.91,"citada":true,…}],"avisos":[],"aviso":"Minuta gerada por IA. Requer revisão e validação por profissional habilitado.","tempoGeracaoMs":48213,"traceId":"…"}
```

Falha depois do stream aberto: `{"tipo":"erro","mensagem":"Não foi possível gerar a peça. Tente novamente.","solicitacaoId":"…"}`.
`"stream": false` devolve um JSON único (`textoGerado`, `fontes`, `avisos`, `aviso`, …).

### Indexar um documento

```bash
curl -X POST "https://api.exemplo.com.br/api/v1/pecas/indexar" \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"documentoOrigemId":"…","nomeArquivo":"contrato.pdf","tipoDocumento":"contrato","area":"civel",
       "tribunal":null,"dataPublicacao":null,"urlOrigem":null,
       "paginas":[{"numero":1,"texto":"Cláusula primeira…"}]}'
```

## Configuração

`appsettings.json` (seções `Drafting`, `Ollama`, `Qdrant` — valores reais **só** por variável de
ambiente/User Secrets, nunca no arquivo versionado):

| Chave | Variável de ambiente | Padrão |
|---|---|---|
| `Drafting:Enabled` | `DRAFTING__ENABLED` | `false` |
| `Ollama:BaseUrl` | `OLLAMA__BASEURL` | placeholder inválido (de propósito) |
| `Ollama:ApiKey` | `OLLAMA__APIKEY` | vazio (token do proxy reverso) |
| `Ollama:Modelo` / `ModeloEmbedding` | `OLLAMA__MODELO` … | `qwen3:8b` / `bge-m3` |
| `Ollama:TimeoutSegundos` | | `600` |
| `Ollama:NumCtx` / `NumPredict` | | `16384` / `4096` |
| `Qdrant:BaseUrl` | `QDRANT__BASEURL` | placeholder inválido — **porta gRPC (6334)** |
| `Qdrant:ApiKey` | `QDRANT__APIKEY` | vazio |
| `Qdrant:Colecao` / `LimiteResultados` | | `juridico_chunks` / `8` (força 5–10) |
| `*:AllowInsecureHttp` | | `false` (ver ADR-072) |

Desenvolvimento local: `appsettings.Development.json` liga o módulo contra `localhost:11434` e
`localhost:6334`; `dotnet user-secrets set "Ollama:BaseUrl" …` para sobrescrever.

## Banco de dados

1. Gerar a migração do EF (não foi gerada: o ambiente de escrita não tinha o SDK):
   ```bash
   dotnet ef migrations add InitialDrafting \
     --project src/Modules/Drafting/Drafting.Infrastructure \
     --startup-project src/Hosts/Api --context DraftingDbContext \
     --output-dir Persistence/Migrations
   dotnet ef database update --project src/Modules/Drafting/Drafting.Infrastructure \
     --startup-project src/Hosts/Api --context DraftingDbContext
   ```
2. Aplicar `infra/supabase/migrations/016_drafting.sql` (RLS e grants; sem DELETE para `app_user`).

Tabelas: `legal_document_requests` (a spec chamava de `LegalDocumentRequest`), `retrieved_sources`
(`RetrievedSource`), `legal_document_events` (trilha). Além dos campos pedidos, a solicitação tem
`tenant_id` (RLS), `versao`, `request_origem_id`, `prompt_versao` e `documentos_ids_json`; a fonte
tem `tenant_id` e `citada`.

## Implantação na Vast.ai (Ollama + Qdrant)

> **Antes de qualquer coisa:** a instância é uma máquina de **terceiro** num marketplace. Não envie
> dado real de cliente sem cumprir o checklist abaixo. Para testar, use fatos fictícios.

1. **Alugar** uma instância com GPU de ≥ 16 GB de VRAM (qwen3:8b em q4 usa ~6 GB; o contexto de
   16k e o BGE-M3 carregado junto pedem folga), disco de ≥ 60 GB, template com Docker. Prefira
   hosts verificados (datacenter) e **instância "on-demand"**, não "interruptible": uma peça de
   minutos não pode ser interrompida no meio.
2. **Não exponha as portas do Ollama (11434) e do Qdrant (6333/6334) direto na internet.** Escolha:
   - **Recomendado:** um proxy reverso na própria instância (Caddy) com TLS e `Authorization: Bearer`,
     publicando só `443`; ou
   - **Túnel** (Cloudflare Tunnel / WireGuard) entre a API e a instância, sem porta pública.
3. **Subir os serviços** (na instância):
   ```bash
   # Qdrant com chave de API e disco persistente
   docker run -d --name qdrant --restart unless-stopped \
     -p 127.0.0.1:6333:6333 -p 127.0.0.1:6334:6334 \
     -e QDRANT__SERVICE__API_KEY="$QDRANT_KEY" \
     -v /workspace/qdrant:/qdrant/storage qdrant/qdrant

   # Ollama
   docker run -d --name ollama --gpus all --restart unless-stopped \
     -p 127.0.0.1:11434:11434 -e OLLAMA_KEEP_ALIVE=10m \
     -v /workspace/ollama:/root/.ollama ollama/ollama
   docker exec ollama ollama pull qwen3:8b
   docker exec ollama ollama pull bge-m3
   ```
4. **Proxy com TLS** (exemplo de `Caddyfile`; troque o domínio e o token):
   ```
   ia.exemplo.com.br {
     @auth header Authorization "Bearer {env.PROXY_TOKEN}"
     handle @auth { reverse_proxy 127.0.0.1:11434 }
     respond 401
   }
   ```
   O Qdrant (gRPC) precisa de `h2`/gRPC no proxy (`reverse_proxy h2c://127.0.0.1:6334`) ou de túnel.
5. **Configurar a API** (variáveis de ambiente do host, nunca no repositório):
   ```
   OLLAMA__BASEURL=https://ia.exemplo.com.br
   OLLAMA__APIKEY=<token do proxy>
   QDRANT__BASEURL=https://qdrant.exemplo.com.br:443
   QDRANT__APIKEY=<chave do Qdrant>
   DRAFTING__ENABLED=true
   ```
6. **Aquecer e testar:** `curl https://ia.exemplo.com.br/api/tags -H "Authorization: Bearer …"`
   (modelos listados); indexar um documento fictício; gerar uma peça com `stream:false`. A
   primeira chamada é lenta (carrega o modelo na GPU).
7. **Parar a instância** quando não estiver em uso: a cobrança é por hora, e o volume `/workspace`
   persiste enquanto o disco existir. Perder a instância sem backup perde o índice vetorial — ele
   é reconstruível reindexando, **o texto das peças fica no Postgres**.

## Checklist de segurança e privacidade

- [ ] **Decisão formal** do responsável (LGPD/OAB) sobre enviar dado de caso a um host de terceiro;
      contrato/termos do provedor de GPU lidos; DPIA atualizado (ADR-072).
- [ ] `OLLAMA__BASEURL` e `QDRANT__BASEURL` em **https://** (ou túnel); `AllowInsecureHttp` **falso**.
- [ ] Portas 11434/6333/6334 **fechadas** para a internet; só o proxy (443) ou o túnel.
- [ ] Token do proxy e chave do Qdrant **fortes**, em Key Vault/variáveis de ambiente, rotacionados.
- [ ] Nenhum segredo, URL ou IP da instância em `appsettings.json` versionado, logs ou frontend.
- [ ] Instância **on-demand**, em provedor/host verificado; disco com dados **apagado** ao encerrar
      a locação (`rm -rf /workspace/*` e destruir a instância).
- [ ] `Drafting:Enabled=false` em ambientes sem o aceite acima (é o padrão).
- [ ] Dado fictício em teste/staging; nunca documentos reais de cliente antes do aceite.
- [ ] RLS aplicado (`016_drafting.sql`) e verificado com `app_user`; **filtro por `tenantId` do
      Qdrant** coberto por teste (`QdrantServiceTests`).
- [ ] Logs sem fatos/pedidos/trechos/texto (só ids, contagens e tempos) — revisar o que o provedor
      de logs captura.
- [ ] Rate limit `ai-generation` ativo (6/min/usuário); alarme para falhas e latência.
- [ ] Interface exibe **"Minuta gerada por IA. Requer revisão e validação por profissional
      habilitada."** e nunca "pronta para protocolo"; `avisos` do evento final são mostrados.
- [ ] Revisão humana obrigatória: a peça não vira ato sem aprovação do advogado (princípio 1).
- [ ] Transparência ao cliente final sobre uso de IA (Recomendação OAB nº 001/2024).
- [ ] Avaliar criptografia de campo para `fatos`/`pedidos`/`texto_gerado` (ADR-068).
- [ ] Rotina de exclusão sob pedido do titular, incluindo os pontos no Qdrant
      (`DeleteByDocumentAsync`) e as linhas de `legal_document_*`.
- [ ] Teste de regressão do system prompt atualizado a cada mudança de `SystemPrompt.Versao`.

## Estado da verificação

- **Nada foi compilado nem executado.** O ambiente em que este código foi escrito não tem SDK .NET;
  as suítes de teste abaixo foram escritas e conferidas "no olho". **Rode `dotnet build` (Release
  trata warnings como erro) e `dotnet test` antes de qualquer outra coisa** e espere corrigir
  alguns erros de compilação.
- A API do cliente Qdrant foi conferida contra a documentação XML do pacote `Qdrant.Client 1.15.1`
  (construtor, `SearchAsync`, `UpsertAsync`, `DeleteAsync`, `Conditions.*`), mas não compilada.
- Os testes de endpoint usam servidor em memória + EF InMemory: **não** exercitam RLS, índices
  nem o Postgres. Os testes de `OllamaService` usam um `HttpMessageHandler` falso: **não** foram
  contra um Ollama de verdade (formato NDJSON conforme a documentação do Ollama).
- O comportamento do qwen3:8b com este system prompt (obediência às 12 regras, qualidade das
  citações) **não foi avaliado**. Faça uma bateria com casos reais anonimizados antes de confiar.

Testes: `Drafting.Application.UnitTests` (PromptBuilder, RAG/contexto, sanitização, injeção,
validação, citações, orquestração, indexador), `Drafting.Infrastructure.UnitTests` (Ollama: NDJSON,
HTTP, timeout, cancelamento, `think`; Qdrant: filtros; persistência), `Drafting.Api.UnitTests`
(autenticação 401/403, validação 400, stream NDJSON, persistência).
